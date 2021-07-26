/*
User input :
The user must provide 2 files (file names MUST be id.txt and indics.txt)
1.
id.txt file that contains only one column with patient IDs
2. 
indics.txt contain the chosen indexes that the user wants to collect
Each structure on a line with a "," to separate the fields
First field is the structure name
if the struct can have several version of structure name, separate them with a ;
If several versions of a structure name are provided AND several structures really
exists with these names, only the first is taken in account

Other fields can be different indexes. Candidates are:
vol --> volume of the structure (Vol and VOL are tolerated)
min --> minimum dose of the structure (Gy) (Min and MIN are tolerated)
max --> max dose of the structure (Gy) (Max and MAX are tolerated)
mean --> mean dose of the structure (Gy) (Mean and MEAN are tolerated)
median --> median dose of the structure (Gy) (Median and MEDIAN are tolerated)
DXX% or DXXcc --> e.g. D95% or D2.5cc : Dose (Gy) recieved by 95% or 2.55 cc of the structure
VXX% or VXXcc --> e.g V49.6% or V49.6cc : Volume in % or cc that recieved 49.6 Gy

No relative doses are allowed. 

In the following exemple the user wants to collect :
The max dose and the Dose (Gy) recieved by 95% of the volume for structure "Coeur"
The max dose for structure "Canal med"
The max dose for structure that can be name "ptvCMI" OR "ptv cmi"  

----
Coeur,max,D95%
Canal med,max
ptvCMI;ptv cmi,max 

Output: (in out/ dir.)
log.txt: main information about the execution
data.csv: all collected data

 */
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Threading;
using System.IO;

namespace VMS.TPS
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                using (Application app = Application.CreateApplication())
                {
                    Execute(app);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }
        }
        static void Execute(Application app)
        {
            List<string> list_patient = new List<string>();
            List<string> list_struct = new List<string>();
            List<string> list_struct_name = new List<string>();
            String line;
            string[] lineElements;
            int verbose;
            verbose = 1;

            int nPatient = 0;  // total number of patient. Must be the number of lines in ip.txt
            int nPatientWithAnAcceptedPlan = 0; // number of patient with at least an accepted plan
            int foundOneAcceptedPlan = 0; // bool, use to count nPatientWithAnAcceptedPlan
            int totalNumberOfPlans = 0;
            int numberOfAcceptedPlans = 0;
            int numberOfPlansForThisPatient = 0;
            int numberOfAcceptedPlansForThisPatient = 0;          
            string idfilename = "id.txt"; // Input file names can not be chosen
            string structsfilename = "indics.txt"; // Input file names can not be chosen
            Structure struct1;

            #region READ THE ID FILE
            if (verbose > 5)
            {
                Console.WriteLine("ID FILE OPEN.....START\n");
                Console.ReadLine();
            }
            // Open a text file to read patient ID. Text file must contains IDs, one by line
            if (!File.Exists(idfilename))
            {
                Console.WriteLine("Can't find file {0}\nPlease ENTER to exit\n", idfilename);
                Console.ReadLine();
                return;
            }
            StreamReader sr = new StreamReader(idfilename);

            line = sr.ReadLine();
            list_patient.Add(line);
            while (line != null)
            {
                line = sr.ReadLine();
                if (line != null)
                    list_patient.Add(line);
            }
            sr.Close();

            if (verbose > 5)
            {
                Console.WriteLine("ID FILE OPEN.....OK\n");
                Console.ReadLine();
            }

            #endregion

            #region READ THE FILE WITH STRUCTURES AND METRICS
            if (verbose > 5)
            {
                Console.WriteLine("METRICS FILE OPEN.....START\n");
                Console.ReadLine();
            }
            // Open a text file to read patient ID. Text file must contains IDs, one by line
            if (!File.Exists(structsfilename))
            {
                Console.WriteLine("Can't find file {0}\nPlease ENTER to exit\n", structsfilename);
                Console.ReadLine();
                return;
            }
            StreamReader srm = new StreamReader(structsfilename);

            if (srm == null)
            {
                Console.WriteLine("Impossible to read {0}\r\n The file is maybe already opened\r\n", structsfilename);
                Console.ReadLine();
                return;
            }


            line = "start";

            while (line != null)
            {
                line = srm.ReadLine();
                if (line != null)
                    list_struct.Add(line);
                if (verbose > 5)
                    Console.WriteLine("line:{0}", line);

                if (line != null)
                {                   
                    lineElements = line.Split(','); // lineElements is a list of the elements of a line 
                    list_struct_name.Add(lineElements[0]); // first column is the structure name
                    if (verbose > 5)
                    {
                        Console.WriteLine("struct:{0}", lineElements[0]);
                        Console.ReadLine();
                    }
                }
            }

            srm.Close();

            if (verbose > 5)
            {
                Console.WriteLine("METRICS FILE OPEN.....OK\n");
                Console.ReadLine();
            }

            #endregion

            #region DELETE AND CREATE OUTPUT DIR
            string folderPath = @"./out";
            if (!Directory.Exists(folderPath)) // if out/ doesn't exist
            {
                Directory.CreateDirectory(folderPath);
                Console.WriteLine("Directory {0} created...", folderPath);
            }
            else // if out/ already exists
            {
                var dir = new DirectoryInfo(folderPath);
                foreach (var file in dir.GetFiles()) // get files one by one in out/ to delete them
                {
                    {
                        try
                        {
                            file.Delete(); // delete this file
                        }
                        catch (IOException)  // This part does not work. If an output file is open the error message is not displayed
                        {
                            Console.WriteLine("Impossible to delete a file (locked). Please close all output files\r\n");
                            Console.ReadLine();
                            return;
                        }
                    }
                }
                Directory.Delete(folderPath); // Remove out/ dir. 
                Directory.CreateDirectory(folderPath); // Re Create out/ dir. 
                Console.WriteLine("Directory {0} deleted and recreated...", folderPath);
            }
            #endregion

            #region CREATE THE OUTPUT FILES         
            // create log file
            StreamWriter swLogFile = new StreamWriter("out/log.txt");
            swLogFile.WriteLine("Output log\r\n\r\n\r\n");
            
            // create file for output data
            StreamWriter swData = new StreamWriter("out/data.csv");    
            
            #region WRITE CSV HEAD
            swData.Write("ID,date,user");  // first 3 fields separated by a coma
            foreach (string myString in list_struct) // loop on the lines
            {
                lineElements = myString.Split(',');  // separate elements in a line by a ,
                string[] myFirstName = lineElements[0].Split(';'); // separate the element (different struct names separate by a ;) 
                foreach (string myOthereMetrics in lineElements.Skip(1)) // Create a cell name: <struct name> (<dose index>)
                    swData.Write(",{0} ({1})", myFirstName[0], myOthereMetrics);
            }
            swData.Write("\r\n"); // add a final line break
            #endregion
            #endregion

            #region LOOP EVERY PATIENT
            foreach (string ipp in list_patient) // loop on the patient list
            {
                nPatient++; // number of patients
                numberOfPlansForThisPatient = 0;
                numberOfAcceptedPlansForThisPatient = 0;
                foundOneAcceptedPlan = 0;

                Patient p = app.OpenPatientById(ipp); // open the patient

                if (verbose > 0)
                {
                    Console.WriteLine("{1} {0}", p.Name, nPatient); // verbose
                    swLogFile.WriteLine("{1} {0}\n\n\n", p.Name, nPatient);
                }
                int keepThisPlan = 1;
                #region LOOP EVERY COURSE
                foreach (Course course in p.Courses) // loop on the courses
                {
                    #region LOOP EVERY PLAN
                    foreach (PlanSetup plan in course.PlanSetups) // loop on the plans
                    {
                        keepThisPlan = 1;
                        totalNumberOfPlans++;
                        numberOfPlansForThisPatient++;
                        Console.WriteLine("Plan: {0} ", plan.Id); // Verbose      
                        swLogFile.WriteLine("Plan: {0} ", plan.Id); // Verbose      

                        #region TEST THE PLAN

                        #region OK IF THE PLAN HAS NO NAME
                        /* if (plan.Name == "")  // For tomo, plan with a name makes the script fail
                             keepThisPlan = keepThisPlan * 1; // Actually it doesn't. This test must be removed
                         else
                         {
                             keepThisPlan = keepThisPlan * 0;
                             Console.WriteLine("         refused: THE PLAN HAS A NAME");
                         }*/
                        #endregion
                        #region OK IF THE PLAN IS UNAPPROVED
                        if (keepThisPlan == 1) // For tomo good plans are unapproved
                            if (plan.ApprovalStatus == PlanSetupApprovalStatus.UnApproved)
                                keepThisPlan = keepThisPlan * 1; // 1321
                            else
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: THE PLAN IS APPROVED");
                                swLogFile.WriteLine("         refused: THE PLAN IS APPROVED");

                            }
                        #endregion
                        #region OK IF TOTAL DOSE > 60
                        if (keepThisPlan == 1)  // check the dose is realistic i.e. > 10
                            if (plan.TotalDose.Dose >= 60.0)
                                keepThisPlan = keepThisPlan * 1;
                            else
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: TOTAL DOSE < 60");
                                swLogFile.WriteLine("         refused: TOTAL DOSE < 60");
                            }
                        #endregion

                        #endregion

                        #region GET THE DATA 

                        if (keepThisPlan == 1)
                        {
                            numberOfAcceptedPlans++;
                            numberOfAcceptedPlansForThisPatient++;

                            if (foundOneAcceptedPlan == 0)
                            {
                                nPatientWithAnAcceptedPlan++;
                                foundOneAcceptedPlan = 1;
                            }
                            else
                            {
                                Console.WriteLine("   THIS PATIENT HAS MORE THAN ONE ACCEPTED PLAN !!"); // verbose
                                swLogFile.WriteLine("   THIS PATIENT HAS MORE THAN ONE ACCEPTED PLAN !!"); // verbose

                            }

                            if (verbose > 0)
                            {
                                Console.WriteLine("   Total dose =  {0}  ", plan.TotalDose); // verbose
                                swLogFile.WriteLine("   Total dose =  {0}  ", plan.TotalDose); // verbose
                            }

                            // write first 3 columns
                            swData.Write("{0},{1},{2}", p.Id, plan.CreationDateTime, plan.CreationUserName);

                            StructureSet ss = plan.StructureSet;
                            bool foundOneStruct = false;
                            foreach (string myString in list_struct) // loop on lines of user dose index (1 by struct)
                            {
                                // get separated elements of a line (separator is a ,)
                                lineElements = myString.Split(',');
                                // get the different possible names of the structure (separate by a ;)
                                string[] myFirstName = lineElements[0].Split(';');  
                                foundOneStruct = false;
                                foreach (string myDiffStrucNames in myFirstName) // loop on the different names of a same struct
                                {
                                    if (foundOneStruct == false)
                                    {
                                        struct1 = ss.Structures.FirstOrDefault(x => x.Id == myDiffStrucNames);
                                        if (struct1 != null) // does the stucture exist?
                                        {
                                            if (!struct1.IsEmpty) // Is it empty?
                                            {
                                                foundOneStruct = true;
                                                DVHData dvh = plan.GetDVHCumulativeData(struct1, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);

                                                swLogFile.WriteLine("{0} found", myDiffStrucNames); // verbose
                                                if (verbose > 0)
                                                    Console.WriteLine(" {0} found", myDiffStrucNames);
                                                foreach (string dataToGet in lineElements.Skip(1)) // loop on index
                                                {
                                                    if (verbose > 5) 
                                                        Console.WriteLine(" Gimme the {0} for {1}\r\n", dataToGet, struct1.Id);

                                                    double thisValueImLookingFor = -99.999;
                     
                                                    thisValueImLookingFor = gimmeThatBro(dataToGet, struct1, plan,dvh);
                                                    
                                                    swLogFile.WriteLine("  {0} for {1} is {2:0.00}", dataToGet, struct1.Id, thisValueImLookingFor);
                                                    swData.Write(",{0:0.00}", thisValueImLookingFor);
                                                }
                                            }
                                        }
                                    }                                                                      
                                }
                                
                                if (foundOneStruct == false)
                                {
                                    Console.WriteLine(" Cannot find the structure {0} with this name or other names", myFirstName[0]);
                                    swLogFile.WriteLine(" Cannot find the structure {0} with this name or other names", myFirstName[0]);
                                    foreach (string skippedData in lineElements.Skip(1))
                                        swData.Write(",");
                                }                              
                            }                            
                        }
                        swData.Write("\r\n");
                        #endregion

                    } //end of plan loop
                    #endregion
                } // end of course loop
                #endregion


                app.ClosePatient();
                Console.WriteLine("For this patient {0}/{1} accepted plans\n", numberOfAcceptedPlansForThisPatient, numberOfPlansForThisPatient);
                swLogFile.WriteLine("For this patient {0}/{1} accepted plans\n", numberOfAcceptedPlansForThisPatient, numberOfPlansForThisPatient);
            } // end of patient loop
            #endregion

            #region FINAL MESSAGE
            if (verbose > 0)
            {
                Console.WriteLine("Number of accepted/total patients: {1}/{0} (accepted : at least one accepted plan)", nPatient, nPatientWithAnAcceptedPlan);
                Console.WriteLine("Number of accepted/total plans: {0}/{1}", numberOfAcceptedPlans, totalNumberOfPlans);
                Console.WriteLine("Pleas type Enter\n");
                Console.ReadLine(); // Ask user to type enter to finish.
                swLogFile.WriteLine("Number of accepted/total patients: {1}/{0} (accepted : at least one accepted plan)", nPatient, nPatientWithAnAcceptedPlan);
                swLogFile.WriteLine("Number of accepted/total plans: {0}/{1}", numberOfAcceptedPlans, totalNumberOfPlans);
                swLogFile.WriteLine("Pleas type Enter\n");

            }
            #endregion

            #region CLOSE FILES
            swLogFile.Close();
            swData.Close();

            #endregion
        }
        public static double gimmeThatBro(string myDataToGet, Structure myStruct, PlanSetup myPlan, DVHData dvh)
        {
            int verbose = 0;
            double checkThat = -1.0;
            if (verbose > 5) 
                Console.WriteLine("--> looking for {0} for {1} in {2}", myDataToGet, myStruct.Id,myPlan.Id);
            #region MAX DOSE       
            if (myDataToGet == "max" || myDataToGet == "Max" || myDataToGet == "MAX")
            {

                var myMaxDose = dvh.MaxDose;
                checkThat = myMaxDose.Dose;
            }
            #endregion
            #region MIN DOSE       
            if (myDataToGet == "min" || myDataToGet == "Min" || myDataToGet == "MIN")
            {
                var myMinDose = dvh.MinDose;
                checkThat = myMinDose.Dose;
            }
            #endregion
            #region MEDIAN DOSE
            if (myDataToGet == "median" || myDataToGet == "Median" || myDataToGet == "MEDIAN")
            {
                DoseValue myMedianDose = myPlan.GetDoseAtVolume(myStruct, 50, VolumePresentation.Relative, DoseValuePresentation.Absolute);              
                checkThat = myMedianDose.Dose;
            }
            #endregion
            #region MEAN DOSE
            if (myDataToGet == "mean" || myDataToGet == "Mean" || myDataToGet == "MEAN")
            {
                var myMeanDose = dvh.MeanDose;
                checkThat = myMeanDose.Dose;
            }
            #endregion
            #region VOLUME
            if (myDataToGet == "vol" || myDataToGet == "Vol" || myDataToGet == "VOL")
            {
                checkThat = myStruct.Volume;
            }
            #endregion
            #region D__% or D__cc
            string d_at_v_pattern = @"^D(?<evalpt>\d+\p{P}\d+|\d+)(?<unit>(%|cc))$"; // matches D95%, D2cc
            var testMatch = Regex.Matches(myDataToGet, d_at_v_pattern);
            if (testMatch.Count != 0) // count is 1 if D95% or D2cc
            {
                Group eval = testMatch[0].Groups["evalpt"];
                Group unit = testMatch[0].Groups["unit"];
                DoseValue.DoseUnit du = DoseValue.DoseUnit.Gy;
                DoseValue myD_something = new DoseValue(1000.1000, du);
                //DoseValue myD_something;
                double myD = Convert.ToDouble(eval.Value);
                if (unit.Value == "%")
                {
                    myD_something = myPlan.GetDoseAtVolume(myStruct, myD, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                    checkThat = myD_something.Dose;
                }
                else if (unit.Value == "cc")
                {
                    myD_something = myPlan.GetDoseAtVolume(myStruct, myD, VolumePresentation.AbsoluteCm3, DoseValuePresentation.Absolute);
                    checkThat = myD_something.Dose;
                }
                else
                    checkThat = -1.0;

                if (verbose > 5)
                    Console.WriteLine("Dxx {0:0.00} {1}", myD_something.Dose, myD_something.Unit);
            }
            #endregion
            #region V__Gy
            string v_at_d_pattern = @"^V(?<evalpt>\d+\p{P}\d+|\d+)(?<unit>(%|cc))$"; // matches V50.4cc or V50.4% 
            //var
            testMatch = Regex.Matches(myDataToGet, v_at_d_pattern);
            if (testMatch.Count != 0) // count is 1
            {
                Group eval = testMatch[0].Groups["evalpt"];
                Group unit = testMatch[0].Groups["unit"];
                DoseValue.DoseUnit du = DoseValue.DoseUnit.Gy;
                DoseValue myRequestedDose = new DoseValue(Convert.ToDouble(eval.Value), du);

                if (unit.Value == "cc")
                    checkThat = myPlan.GetVolumeAtDose(myStruct, myRequestedDose, VolumePresentation.AbsoluteCm3);
                else if (unit.Value == "%") 
                    checkThat = myPlan.GetVolumeAtDose(myStruct, myRequestedDose, VolumePresentation.Relative);
                else
                    checkThat = -1.0;
            }
            #endregion
            if (checkThat == -1.0)
                Console.WriteLine("Impossible to obtain {0} for {1} in {2} ", myDataToGet, myStruct.Id, myPlan.Id);
            return (checkThat);
        }
    }
}
