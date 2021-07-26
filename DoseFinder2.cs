/*
 
Read an ID.txt file that contains only one column with patient IDs
Read a file with these lines : 

Coeur,max,D95%
Canal med,max
ptvCMI;ptv cmi,max 

Each column separated by a ,
Several names of structures can be specified with ;

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
            string idfilename = "id.txt";
            string structsfilename = "indics.txt";

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
                    //string[] lineElements = line.Split(',');
                    lineElements = line.Split(',');
                    list_struct_name.Add(lineElements[0]);
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
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Console.WriteLine("Directory {0} created...", folderPath);
            }
            else
            {
                //string[] files = Directory.GetFiles(folderPath);
                //foreach (string file in files)
                var dir = new DirectoryInfo(folderPath);
                foreach (var file in dir.GetFiles())
                {
                    {
                        try
                        {
                            file.Delete();
                            //                            File.Delete(file);
                        }
                        catch (IOException)  // This part does not work. If an output file is open the error message is not displayed
                        {
                            Console.WriteLine("Impossible to delete a file (locked). Please close all output files\r\n");
                            Console.ReadLine();
                            return;

                        }


                    }
                }
                Directory.Delete(folderPath);
                Directory.CreateDirectory(folderPath);
                Console.WriteLine("Directory {0} deleted and recreated...", folderPath);
            }
            #endregion

            #region CREATE THE OUTPUT FILES
            // Open text files to write output
            StreamWriter swLogFile = new StreamWriter("out/log.txt");
            swLogFile.WriteLine("Output log\n\n\n");

            StreamWriter swData = new StreamWriter("out/data.csv");
            //swHeart.WriteLine("ID,volume(cc),meanDose(Gy),medianDose(Gy),V40Gy(cc),date,user");


            #region WRITE CSV HEAD
            swData.Write("ID,date,user");  // first 3 fields separated by a coma
            foreach (string myString in list_struct)
            {
                lineElements = myString.Split(',');  // separate elements in a line by a ,
                string[] myFirstName = lineElements[0].Split(';'); // separate the element (different struc names separate by a ;) 
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
                            StructureSet ss = plan.StructureSet;















                            #region COEUR
                            string myStructName = "Coeur";
                            Structure s = ss.Structures.FirstOrDefault(x => x.Id == myStructName);
                            if (s != null)
                            {
                                if (!s.IsEmpty)
                                {
                                    DVHData dvh = plan.GetDVHCumulativeData(s, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                                    // get the volume
                                    double myVol = s.Volume;
                                    // get median dose to Coeur
                                    DoseValue myMedianDose = plan.GetDoseAtVolume(s, 50, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                                    // get mean dose
                                    var myMeanDose = dvh.MeanDose;

                                    // get V40Gy (cc)
                                    DoseValue.DoseUnit du = DoseValue.DoseUnit.Gy;
                                    DoseValue my40Gy = new DoseValue(40.0, du);
                                    double V40GyCoeurcc = plan.GetVolumeAtDose(s, my40Gy, VolumePresentation.AbsoluteCm3);

                                    //                                    swHeart.WriteLine("{0},{1:0.000},{2:0.000},{3:0.000},{4:0.000}", p.Id, plan.CreationDateTime, plan.CreationUserName, myVol, myMeanDose.Dose, myMedianDose.Dose, V40GyCoeurcc);
                                    swData.WriteLine("{0},{1:0.000},{2:0.000},{3:0.000},{4:0.000},{5},{6}", p.Id, myVol, myMeanDose.Dose, myMedianDose.Dose, V40GyCoeurcc, plan.CreationDateTime, plan.CreationUserName);

                                    if (verbose > 3)
                                        Console.WriteLine("heart ok");
                                }
                            }
                            else if (s == null || s.IsEmpty)
                            {
                                Console.WriteLine("*** Can not find {0} for patient {1}", myStructName, ipp);
                                Console.WriteLine("*** Please check...");
                                Console.WriteLine("*** Program is ending, type ENTER");
                                Console.ReadLine();
                                return;
                            }
                            #endregion

                        }

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
    }
}
