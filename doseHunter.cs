/*
User input :
The user must provide 3 files (file names MUST be id.txt and index.txt)
See documentation 

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
using System.Linq.Expressions;
using System.Windows.Controls;
using static VMS.TPS.Program;
using System.IO.Packaging;
using System.Diagnostics;
using System.Reflection;

namespace VMS.TPS
{
    class Program
    {
        #region Calculate DVH 
        public class DVH
        {
            public DVH() {; }
            #region CalculeDvh
            internal DVHData CalculateDvh(PlanUncertainty uncert_plan, Structure structure, string volume_representation) // Function de generate DVHData variable
            {

                if (volume_representation.ToUpper() == "RELATIVE")
                {
                    return uncert_plan.GetDVHCumulativeData(structure,
                    DoseValuePresentation.Absolute,
                    VolumePresentation.Relative, 0.001);
                }
                else
                {
                    return uncert_plan.GetDVHCumulativeData(structure,
                    DoseValuePresentation.Absolute,
                    VolumePresentation.AbsoluteCm3, 0.001);
                }
            }
            #endregion
            #region extract_VolumeAtDose
            internal double extract_VolumeAtDose(List<(double, double)> dvh_table, string Value, PlanSetup MyPlan, Structure MyStruct) // Function to calculate the Volume at certain Dose (Gy) from the DVHData
            {
                double closestVolume = 0;
                double VolumeAsked = 0.0000, DoseAsked = 0.0000;
                double PresDose = MyPlan.TotalDose.Dose;
                double toleranceV = 0.01, ToleranceD = 0.01;
                double StructVol = MyStruct.Volume;
                List<(double, double)> AverageDVH = new List<(double, double)>();
                List<(double, double)> DvhDiff = new List<(double, double)>();

              

                #region Mean, Median, Min et Max
                if (Value.ToUpper() == "MEAN") // HDV cumule
                {
                    double Volcc = 0;
                    for (int i = dvh_table.Count - 1; i > 0; i--)
                    {
                        Volcc = (1 - ((dvh_table[i].Item2/100) * StructVol)) - (1 - ((dvh_table[i-1].Item2/ 100) * StructVol));
                        DvhDiff.Add((dvh_table[i].Item1,Volcc));
                    }
                    closestVolume = DvhDiff.Sum(x => x.Item1 * x.Item2) / StructVol;
                    return closestVolume;
                }
                if (Value.ToUpper() == "MEDIAN")
                {
                    closestVolume = dvh_table.Where(pair => Math.Abs(pair.Item2) <= 50 + toleranceV && Math.Abs(pair.Item2 - VolumeAsked) >= 50 - toleranceV).Select(pair => pair.Item1).FirstOrDefault();
                    return closestVolume;
                }
                if (Value.ToUpper() == "MIN")
                {
                    closestVolume = dvh_table.Where(pair => pair.Item2 == 100).Select(pair => pair.Item1).LastOrDefault();
                    return closestVolume;
                }
                if (Value.ToUpper() == "MAX")
                {
                    closestVolume = dvh_table.Max(pair => pair.Item1);
                    return closestVolume;
                }
                #endregion
                #region D__% or D__cc
                try
                {
                    if (Value.Contains("D")) // ne fonctionne pas sans cette condition
                    {
                        string d_at_v_pattern = @"^D(?<evalpt>\d+\p{P}\d+|\d+)(?<unit>(%|cc))$"; // matches D95%, D2cc
                        var testMatch = Regex.Matches(Value, d_at_v_pattern);
                        Group eval = testMatch[0].Groups["evalpt"];
                        Group unit = testMatch[0].Groups["unit"];
                        double.TryParse(eval.Value, out VolumeAsked);

                        if (testMatch.Count != 0)
                        {
                            if (unit.Value == "%") //Relative volume
                            {
                                closestVolume = dvh_table.Where(pair => Math.Abs(pair.Item2 - VolumeAsked) < toleranceV).Select(pair => pair.Item1).FirstOrDefault();
                                if (VolumeAsked == 100) // permet de gérer les multiples valeurs de dose pour le 100% de couverture dans l'HDV cumulatif
                                {
                                    closestVolume = dvh_table.Where(pair => Math.Abs(pair.Item2 - VolumeAsked) < toleranceV).Select(pair => pair.Item1).LastOrDefault();
                                }
                            }
                            else if (unit.Value == "cc") //Absolute volume
                            {
                                closestVolume = dvh_table.Where(pair => Math.Abs((pair.Item2 / 100) * StructVol - VolumeAsked) < toleranceV).Select(pair => pair.Item1).FirstOrDefault();
                            }
                            return closestVolume;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString(), " for {0} ", Value);
                }
                #endregion
                #region V__Gy
                try
                {
                    if (Value.Contains("V")) // ne fonctionne pas sans cette condition
                    {
                        string v_at_d_pattern = @"^V(?<evalpt>\d+\p{P}\d+|\d+)(?<unit>(%|cc))$"; // matches V50.4cc or V50.4%                                                                                            
                        var testMatch = Regex.Matches(Value, v_at_d_pattern);
                        Group eval = testMatch[0].Groups["evalpt"];
                        Group unit = testMatch[0].Groups["unit"];
                        double.TryParse(eval.Value, out DoseAsked);

                        if (testMatch.Count != 0) // count is 1
                        {
                            if (unit.Value == "%") //Relative Volume
                            {
                                closestVolume = dvh_table.Where(pair => Math.Abs(pair.Item1 - DoseAsked) < ToleranceD).Select(pair => pair.Item2).FirstOrDefault();
                                if (VolumeAsked == 100) // permet de gérer les multiples valeurs de couverture pour la dose dans l'HDV cumulatif
                                {
                                    closestVolume = dvh_table.Where(pair => Math.Abs(pair.Item1 - DoseAsked) < ToleranceD).Select(pair => pair.Item2).LastOrDefault();
                                }
                            }
                            else if (unit.Value == "cc") //Absolute volume
                            {
                                closestVolume = dvh_table.Where(pair => Math.Abs(pair.Item1 - DoseAsked) < ToleranceD).Select(pair => pair.Item2 * StructVol / 100).FirstOrDefault();
                            }
                            return closestVolume;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString(), " for {0} ", Value);
                }
                return closestVolume;
            }
            #endregion
            #endregion
            #region CurveData2Table
            internal List<(double, double)> CurveData2Table(DVHData dvh_data)
            {
                List<(double, double)> dvh_table = new List<(double, double)>(); //Table that will store dvh curve
                //Place the DVH Data into a table
                foreach (DVHPoint dvh_points in dvh_data.CurveData)
                {
                    dvh_table.Add((dvh_points.DoseValue.Dose, dvh_points.Volume));
                }
                return dvh_table;
            }
            #endregion
        }
        #endregion
        [STAThread]
        #region EMPTY MAIN PROGRAM

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
                Console.ReadLine();
            }
        }
        #endregion

        #region EXECUTE PROGRAM, THE REAL MAIN
        static void Execute(Application app)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            #region WELCOME MESSAGE
            Console.WriteLine("-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-");
            Console.WriteLine("");
            Console.WriteLine("     D O S E   H U N T E R ");
            Console.WriteLine("");
            Console.WriteLine("        Luc SIMON, 2021");
            Console.WriteLine("");
            Console.WriteLine("-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-");
            #endregion

            #region DECLARATION OF VARIABLES
            List<string> list_patient = new List<string>();
            List<string> list_struct = new List<string>();
            List<string> list_struct_name = new List<string>();
            String line;
            string[] lineElements;
            string[] filterTags;
            string[] stringToContainToBeKept = null;
            string[] stringinCourseToContainToBeKept = null;
            string[] stringToContainToBeExcluded = null;
            bool isADoublon = false;
            int verbose;
            //verbose = 9;
            verbose = 1;
            DateTime lastTime = DateTime.Now;
            int nPatient = 0;  // total number of patient. Must be the number of lines in ip.txt
            int nDoublons = 0;
            int nPatientWithAnAcceptedPlan = 0; // number of patient with at least an accepted plan
            int foundOneAcceptedPlan = 0; // bool, use to count nPatientWithAnAcceptedPlan
            int totalNumberOfPlans = 0;
            int numberOfAcceptedPlans = 0;
            int numberOfPlansForThisPatient = 0;
            int numberOfAcceptedPlansForThisPatient = 0;
            string idfilename = "id.txt"; // Input file names can not be chosen
            string structsfilename = "index.txt"; // Input file names can not be chosen
            string planfilterfilename = "planfilter.txt"; // Input file names can not be chosen
            Structure struct1;
            double minTotalDose, maxTotalDose;
            bool keepUnapprovedPlan, keepPAapprovedPlan, keepTAapprovedPlan;
            bool keepNamedPlan, keepUnamedPlan, keepRefusedPlan, keepRetiredPlan;
            bool keepIfPlanNameContainAstring, excludeIfPlannedNameContainAString;
            bool keepIfCourseNameContainAstring;
            bool exploreSumPlan;
            bool exploreUP;
            #endregion

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
            int nPatientsInList = 0;
            StreamReader sr = new StreamReader(idfilename);
            line = sr.ReadLine(); // read first line
            if ((line != null) && (line.Length > 2)) // an ID Must be > 2 characters
            {
                nPatientsInList++;
                list_patient.Add(line);
            }
            while (line != null)
            {
                line = sr.ReadLine();
                #region TEST IF ID IS A DOUBLON
                foreach (string ipp in list_patient) // loop on the patient list
                {
                    if (isADoublon == false)
                        if (ipp == line)
                            isADoublon = true;
                }
                #endregion
                if ((line != null) && (line.Length > 2)) // an ID Must be > 2 characters
                    if (isADoublon == false)
                    {
                        nPatientsInList++;
                        list_patient.Add(line);
                    }
                    else
                    {
                        nDoublons++;
                        isADoublon = false;
                        //Console.WriteLine(" {0} is a doublon\n", line);
                    }
            }


            sr.Close();

            if (verbose > 5)
            {
                Console.WriteLine("ID FILE OPEN.....OK\n");
                Console.ReadLine();
            }

            #endregion

            #region READ THE PLAN FILTER FILE
            if (verbose > 5)
            {
                Console.WriteLine("PLAN FILTER FILE OPEN.....START\n");
                Console.ReadLine();
            }
            // Open a text file to read patient ID. Text file must contains IDs, one by line

            // DEFAULT FILTER VALUES :
            minTotalDose = 60.0;
            maxTotalDose = 80.0;
            keepNamedPlan = true;
            keepUnamedPlan = true;
            keepRefusedPlan = true;
            keepRetiredPlan = true;
            keepPAapprovedPlan = false;
            keepTAapprovedPlan = false;
            keepUnapprovedPlan = true;
            keepIfPlanNameContainAstring = false;
            excludeIfPlannedNameContainAString = false;
            exploreSumPlan = false;
            exploreUP = false;
            keepIfCourseNameContainAstring = false;

            if (!File.Exists(planfilterfilename))
            {
                Console.WriteLine("Can't find file {0}\r\n", planfilterfilename);
                Console.WriteLine("Default filters will be used\r\n");

                Console.ReadLine();

            }
            else
            {
                StreamReader srf = new StreamReader(planfilterfilename);
                line = "start";
                while (line != null)
                {
                    line = srf.ReadLine();
                    if (line != null)
                    {
                        filterTags = line.Split(':');
                        if (filterTags[0] == "Min Total Dose (Gy)")
                        {
                            minTotalDose = Convert.ToDouble(filterTags[1]);
                        }
                        if (filterTags[0] == "Max Total Dose (Gy)")
                        {
                            maxTotalDose = Convert.ToDouble(filterTags[1]);
                        }
                        if (filterTags[0] == "TreatApproved plan")
                        {
                            if (filterTags[1].ToLower() == "yes")
                                keepTAapprovedPlan = true;
                            else if (filterTags[1].ToLower() == "no")
                                keepTAapprovedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "PlanningApproved plan")
                        {
                            if (filterTags[1].ToLower() == "yes")
                                keepPAapprovedPlan = true;
                            else if (filterTags[1].ToLower() == "no")
                                keepPAapprovedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Unapproved plan")
                        {
                            if (filterTags[1].ToLower() == "yes")
                                keepUnapprovedPlan = true;
                            else if (filterTags[1].ToLower() == "no")
                                keepUnapprovedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Named plan")
                        {
                            if (filterTags[1].ToLower() == "yes")
                                keepNamedPlan = true;
                            else if (filterTags[1].ToLower() == "no")
                                keepNamedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Unnamed plan")
                        {
                            if (filterTags[1].ToLower() == "yes")
                                keepUnamedPlan = true;
                            else if (filterTags[1].ToLower() == "no")
                                keepUnamedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Refused plan")
                        {
                            if (filterTags[1].ToLower() == "yes")
                                keepRefusedPlan = true;
                            else if (filterTags[1].ToLower() == "no")
                                keepRefusedPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Retired plan")
                        {
                            if (filterTags[1].ToLower() == "yes")
                                keepRetiredPlan = true;
                            else if (filterTags[1].ToLower() == "no")
                                keepRetiredPlan = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Plan name must contain a string")
                        {
                            if (filterTags[1].ToLower() == "yes")
                            {
                                keepIfPlanNameContainAstring = true;
                                stringToContainToBeKept = filterTags;
                            }
                            else if (filterTags[1].ToLower() == "no")
                                keepIfPlanNameContainAstring = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Exclude if plan name contains")
                        {
                            if (filterTags[1].ToLower() == "yes")
                            {
                                excludeIfPlannedNameContainAString = true;
                                stringToContainToBeExcluded = filterTags;
                            }
                            else if (filterTags[1].ToLower() == "no")
                                excludeIfPlannedNameContainAString = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }
                        if (filterTags[0] == "Explore Sumplans")
                        {
                            if (filterTags[1].ToLower() == "yes")
                            {
                                exploreSumPlan = true;
                            }
                            else if (filterTags[1].ToLower() == "no")
                            {
                                exploreSumPlan = false;
                            }
                        }
                        if (filterTags[0] == "Explore uncertainty")
                        {
                            if (filterTags[1].ToLower() == "yes")
                            {
                                exploreUP = true;
                            }
                            else if (filterTags[1].ToLower() == "no")
                            {
                                exploreUP = false;
                            }
                        }

                        if (filterTags[0] == "Course name must contain a string")
                        {
                            if (filterTags[1].ToLower() == "yes")
                            {
                                keepIfCourseNameContainAstring = true;
                                stringinCourseToContainToBeKept = filterTags;
                            }
                            else if (filterTags[1].ToLower() == "no")
                                keepIfCourseNameContainAstring = false;
                            else
                                Console.WriteLine("*** Unexpected value for filter '{0}'", filterTags[0]);
                        }

                    }
                }
                srf.Close();
            }


            #region RECAP FILTERS
            Console.WriteLine("\r\n\r\nPlans filters. The following filters will be used to select the plans:");
            Console.WriteLine("Total dose between {0:0.00} and {1:0.00} Gy", minTotalDose, maxTotalDose);
            Console.WriteLine("Keep planning approved plans?\t{0}", keepPAapprovedPlan);
            Console.WriteLine("Keep treatment approved plans?\t{0}", keepTAapprovedPlan);
            Console.WriteLine("Keep retired plans?\t{0}", keepRetiredPlan);
            Console.WriteLine("Keep refused plans?\t{0}", keepRefusedPlan);
            Console.WriteLine("Keep unapproved plans?\t{0}", keepUnapprovedPlan);
            Console.WriteLine("Keep named plans?\t{0}", keepNamedPlan);
            Console.WriteLine("Keep unamed plans?\t{0}", keepUnamedPlan);
            Console.WriteLine("Keep plans containing a particular string?\t{0}", keepIfPlanNameContainAstring);
            Console.WriteLine("Explore Sum plans?\t{0}", exploreSumPlan);

            if (keepIfPlanNameContainAstring)
                for (int i = 2; i < stringToContainToBeKept.Length; i++)
                {
                    Console.WriteLine(" keep the plan if planID contains: '{0}'", stringToContainToBeKept[i]);
                }
            Console.WriteLine("Exclude plans containing a particular string?\t{0}", excludeIfPlannedNameContainAString);
            if (excludeIfPlannedNameContainAString)
                for (int i = 2; i < stringToContainToBeExcluded.Length; i++)
                {
                    Console.WriteLine(" exclude the plan if planID contains: '{0}'", stringToContainToBeExcluded[i]);
                }
            Console.WriteLine("\r\n\r\n\r\n");
            #endregion

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
                        // Console.ReadLine();
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

            #region DELETE AND RECREATE OUTPUT DIR
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
            //swData.Write("ID,date,user");  // first 3 fields separated by a coma
            //swData.Write("patientID;planID;date;user");  // first 3 fields separated by a ;
            swData.Write("patientID;courseID;planID;date;user;TotalDose;Dose/#;Fractions;MU;MI;Normalisation");// first 11 fields separated by a ;
            foreach (string myString in list_struct) // loop on the lines
            {
                lineElements = myString.Split(',');  // separate elements in a line by a ,
                string[] myFirstName = lineElements[0].Split(';'); // separate the element (different struct names separate by a ;) 
                foreach (string myOthereMetrics in lineElements.Skip(1)) // Create a cell name: <struct name> (<dose index>)
                    swData.Write(";{0}({1})", myFirstName[0], myOthereMetrics);
                //swData.Write(",{0} ({1})", myFirstName[0], myOthereMetrics);
            }
            swData.Write("\r\n"); // add a final line break
            if (verbose > 5)
            {
                Console.WriteLine("OUTPUT FILE INITATED.....OK\n");
                Console.ReadLine();
            }
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
                    Console.WriteLine("{1}/{2} {0}", p.Name, nPatient, nPatientsInList); // verbose
                    swLogFile.WriteLine("{1}/{2} {0}\n\n\n", p.Name, nPatient, nPatientsInList);
                    if (verbose > 5)
                        Console.ReadLine();
                }
                int keepThisPlan = 1;
                int keepThisCourse = 1;
                #region LOOP EVERY COURSE
                #region TEST THE COURSE
                foreach (Course course in p.Courses) // loop on the courses
                {
                    if (keepIfCourseNameContainAstring)
                    {
                        keepThisCourse = 0;
                        for (int i = 2; i < stringinCourseToContainToBeKept.Length; i++)
                        {
                            if (course.Id.Contains(stringinCourseToContainToBeKept[i]))
                                keepThisCourse = 1;
                        }
                        if (keepThisCourse == 0)
                        {
                            Console.WriteLine("         refused: course ID ({0}) does not contain one of the required strings", course.Id);
                            swLogFile.WriteLine("         refused: course ID ({0}) does not contain one of the required strings", course.Id);
                        }
                    }
                    #endregion

                    #region LOOP EVERY PLAN
                    foreach (PlanSetup plan in course.PlanSetups) // loop on the plans
                    {
                        #region VERBOSE
                        if (verbose > 5)
                        {
                            Console.WriteLine("Inspect Course: {0} Plan: {1}", course.Id, plan.Id); // verbose
                            Console.ReadLine();
                        }
                        #endregion

                        keepThisPlan = 1;
                        if (keepThisCourse == 0)
                            keepThisPlan = 0;
                        totalNumberOfPlans++;
                        numberOfPlansForThisPatient++;
                        Console.WriteLine("  Plan: {0} (course: {1})", plan.Id, course.Id); // Verbose      
                        swLogFile.WriteLine("  Plan: {0}  (course: {1})", plan.Id, course.Id); // Verbose      


                        #region TEST THE PLAN

                        #region EXCLUDE ALL PLANS WITH NO BEAM
                        try                     // this exception
                        {
                            plan.Beams.Count(); // manages plans with no beams. they make the program crash
                        }
                        catch
                        {
                            Console.WriteLine("         refused: THE PLAN HAS NO BEAM");
                            swLogFile.WriteLine("         refused: THE PLAN HAS NO BEAM ");
                            keepThisPlan = 0;
                        }
                        #endregion
                        #region EXCLUDE ALL PLANS WITH NO VALID DOSE
                        if (keepThisPlan == 1)
                        {
                            if (plan.IsDoseValid == false)
                            {
                                Console.WriteLine("         refused: THE PLAN HAS NO VALID DOSE");
                                swLogFile.WriteLine("         refused: THE PLAN HAS NO NO VALID DOSE ");
                                keepThisPlan = 0;
                            }
                        }
                        #endregion


                        #region TEST IF THE PLAN HAS A NAME

                        if (keepThisPlan == 1)
                        {
                            if (keepNamedPlan == false) // dont keep  plans with a name
                            {
                                if (plan.Name != "")  // if  name exist
                                {
                                    keepThisPlan = keepThisPlan * 0;
                                    Console.WriteLine("         refused: THE PLAN HAS A NAME");
                                    swLogFile.WriteLine("         refused: THE PLAN HAS A NAME ");
                                }
                            }
                            if (keepUnamedPlan == false) // dont keep plans with no name
                            {
                                if (plan.Name == "")  // if  no name 
                                {
                                    keepThisPlan = keepThisPlan * 0;
                                    Console.WriteLine("         refused: THE PLAN HAS NO NAME");
                                    swLogFile.WriteLine("         refused: THE PLAN HAS NO NAME ");
                                }
                            }
                        }
                        #endregion
                        #region TEST THE PLAN APPROBATION
                        if (keepThisPlan == 1)
                            if (keepTAapprovedPlan == false) // dont keep  Treat approved plans
                            {
                                if (plan.ApprovalStatus == PlanSetupApprovalStatus.TreatmentApproved)  // if  treat approve
                                {

                                    keepThisPlan = keepThisPlan * 0;
                                    Console.WriteLine("         refused: THE PLAN IS TREAT APPROVED");
                                    swLogFile.WriteLine("         refused: THE PLAN IS TREAT APPROVED");
                                }
                            }
                        if (keepThisPlan == 1)
                            if (keepPAapprovedPlan == false) // dont keep  planning approved plans
                            {
                                if (plan.ApprovalStatus == PlanSetupApprovalStatus.PlanningApproved)  // if  plan approve
                                {
                                    keepThisPlan = keepThisPlan * 0;
                                    Console.WriteLine("         refused: THE PLAN IS PLAN APPROVED");
                                    swLogFile.WriteLine("         refused: THE PLAN IS PLAN APPROVED");
                                }
                            }
                        if (keepThisPlan == 1)
                            if (keepUnapprovedPlan == false) // dont keep   unapproved plans
                            {
                                if (plan.ApprovalStatus == PlanSetupApprovalStatus.UnApproved)  // if  plan approve
                                {
                                    keepThisPlan = keepThisPlan * 0;
                                    Console.WriteLine("         refused: THE PLAN IS UNAPPROVED");
                                    swLogFile.WriteLine("         refused: THE PLAN IS UNAPPROVED");
                                }
                            }
                        if (keepThisPlan == 1)
                            if (keepRefusedPlan == false) // dont keep refused plans
                            {
                                if (plan.ApprovalStatus == PlanSetupApprovalStatus.Rejected)  // if  plan rejected
                                {
                                    keepThisPlan = keepThisPlan * 0;
                                    Console.WriteLine("         refused: THE PLAN STATUS IS REFUSED");
                                    swLogFile.WriteLine("         refused: THE PLAN STATUS IS REFUSED");
                                }
                            }
                        if (keepThisPlan == 1)
                            if (keepRetiredPlan == false) // dont keep retired plans
                            {
                                if (plan.ApprovalStatus == PlanSetupApprovalStatus.Retired)  // if  plan retired
                                {
                                    keepThisPlan = keepThisPlan * 0;
                                    Console.WriteLine("         refused: THE PLAN STATUS IS RETIRED");
                                    swLogFile.WriteLine("         refused: THE PLAN STATUS IS RETIRED");
                                }
                            }

                        #endregion
                        #region TEST IF TOTAL DOSE BETWEEN MIN AND MAX
                        if (keepThisPlan == 1)
                            if (plan.TotalDose.Dose < minTotalDose || plan.TotalDose.Dose > maxTotalDose)
                            {
                                keepThisPlan = keepThisPlan * 0;
                                Console.WriteLine("         refused: Total dose ({0:0.00} Gy) is not between {1} and {2} Gy", plan.TotalDose.Dose, minTotalDose, maxTotalDose);
                                swLogFile.WriteLine("         refused: Total dose ({0:0.00} Gy) is not between {1} and {2} Gy", plan.TotalDose.Dose, minTotalDose, maxTotalDose);
                            }
                        #endregion

                        #region TEST IF PLAN CONTAINS OR DOES NOT CONTAIN SOME CHOSEN STRINGS
                        if (keepThisPlan == 1) // TEST ALL STRINGS TO KEEP PLANS
                            if (keepIfPlanNameContainAstring)
                            {
                                keepThisPlan = 0;
                                for (int i = 2; i < stringToContainToBeKept.Length; i++)
                                {
                                    if (plan.Id.Contains(stringToContainToBeKept[i]))
                                        keepThisPlan = 1;
                                }
                                if (keepThisPlan == 0)
                                {
                                    Console.WriteLine("         refused: plan ID ({0}) does not contain one of the required strings", plan.Id);
                                    swLogFile.WriteLine("         refused: plan ID ({0}) does not contain one of the required strings", plan.Id);
                                }
                            }

                        if (keepThisPlan == 1)  // TEST ALL STINGS TO EXCLUDE PLANS
                            if (excludeIfPlannedNameContainAString)
                            {
                                for (int i = 2; i < stringToContainToBeExcluded.Length; i++)
                                {
                                    if (plan.Id.Contains(stringToContainToBeExcluded[i]))
                                    {
                                        keepThisPlan = keepThisPlan * 0;
                                        Console.WriteLine("         refused: plan ID ({0}) does contain the strings {1}", plan.Id, stringToContainToBeExcluded[i]);
                                        swLogFile.WriteLine("         refused: plan ID ({0}) does contain the strings {1}", plan.Id, stringToContainToBeExcluded[i]);
                                    }
                                }
                            }

                        #endregion

                        #endregion


                        #region VERBOSE
                        if (verbose > 5)
                        {
                            Console.WriteLine("Plan tested"); // verbose
                            Console.ReadLine();
                        }
                        #endregion
                        #region Uncertainty plan
                        // uncertainties analysis must be done before plan analysis. Bug in ESAPI15. 
                        // see reddit thread:
                        // https://www.reddit.com/r/esapi/comments/13ok5g2/plan_uncertainty_dvh_bug/
                        if (exploreUP)
                        {
                            foreach (var pu in plan.PlanUncertainties)
                            {
                                if (pu != null)
                                {
                                    double MU = 0;

                                    foreach (Beam beam in plan.Beams)
                                    {
                                        if (!beam.IsSetupField)
                                            MU = MU + Math.Round(beam.Meterset.Value, 2);
                                    }
                                    //MI
                                    var MI = Math.Round(MU / (plan.DosePerFraction.Dose), 3);
                                    DVH MyDVH = new DVH();

                                    swData.Write("{0};{1};{2} ({11});{3};{4};{5};{6};{7};{8};{9};{10}",
                                    p.Id, course.Id, pu.Id, plan.CreationDateTime, plan.CreationUserName, plan.TotalDose.ValueAsString, plan.DosePerFraction.ValueAsString,
                                    plan.NumberOfFractions, MU, MI, Math.Round(plan.PlanNormalizationValue, 1), plan.Id);

                                    foreach (string myString in list_struct) // loop on lines of user dose index (1 by struct)
                                    {
                                        // get separated elements of a line (separator is a ,)
                                        lineElements = myString.Split(',');
                                        // get the different possible names of the structure (separate by a ;)
                                        string[] myFirstName = lineElements[0].Split(';');

                                        StructureSet ss = plan.StructureSet;
                                        bool foundOneStruct = false;
                                        foreach (string myDiffStrucNames in myFirstName) // loop on the different names of a same struct
                                        {
                                            if (foundOneStruct == false)
                                            {
                                                struct1 = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == myDiffStrucNames.ToLower());
                                                if (struct1 != null) // does the stucture exist?
                                                {
                                                    if (!struct1.IsEmpty) // Is it empty?
                                                    {
                                                        foundOneStruct = true;
                                                        try
                                                        {
                                                            DVHData DVH_uncert = MyDVH.CalculateDvh(pu, struct1, "relative");
                                                            List<(double, double)> dvhPU = MyDVH.CurveData2Table(DVH_uncert);

                                                            if (!dvhPU.Equals(null))
                                                            {

                                                                swLogFile.WriteLine("    {0} found in {1}", myDiffStrucNames, pu.Id); // verbose
                                                                if (verbose > 0)
                                                                    Console.WriteLine("    {0} found in {1} ({2})", myDiffStrucNames, pu.Id, plan.Id);
                                                                foreach (string dataToGet in lineElements.Skip(1)) // loop on index
                                                                {
                                                                    if (verbose > 5)
                                                                        Console.WriteLine(" Gimme the {0} for {1}\r\n", dataToGet, struct1.Id);

                                                                    double thisValueImLookingFor = -99.999;

                                                                    thisValueImLookingFor = MyDVH.extract_VolumeAtDose(dvhPU, dataToGet, plan, struct1);
                                                                    if (thisValueImLookingFor != -1.0)
                                                                    {
                                                                        swLogFile.WriteLine("  {0} for {1} is {2:0.00}", dataToGet, struct1.Id, thisValueImLookingFor);
                                                                        swData.Write(";{0:0.00}", Math.Round(thisValueImLookingFor, 5));
                                                                    }
                                                                    else
                                                                    {
                                                                        swData.Write(";"); // if data impossible to extract
                                                                    }
                                                                }
                                                                dvhPU.Clear();
                                                            }
                                                            else
                                                            {
                                                                Console.WriteLine("DVH is empty");
                                                                swLogFile.WriteLine("DVH is empty");
                                                            }
                                                        }

                                                        catch (Exception ex)
                                                        {
                                                            swLogFile.WriteLine(" Uncertainty plan not found; exception  {0} {1} plan", ex.Message, pu.Id);
                                                            Console.WriteLine(" Uncertainty plan not found; exception  {0} pour le plan {1}", ex, pu.Id);
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        if (foundOneStruct == false)
                                        {
                                            Console.WriteLine("    {0} not found ********", myFirstName[0]);
                                            swLogFile.WriteLine("    {0} not found ********", myFirstName[0]);
                                            foreach (string skippedData in lineElements.Skip(1))
                                                swData.Write(";");
                                            //swData.Write(",");
                                        }
                                    }
                                }
                                swData.WriteLine(";");
                            }
                        }
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
                            //Total MU
                            double MU = 0;
                            foreach (Beam beam in plan.Beams)
                            {
                                if (!beam.IsSetupField)
                                    MU = MU + Math.Round(beam.Meterset.Value, 2);
                            }
                            //MI
                            var MI = Math.Round(MU / (plan.DosePerFraction.Dose), 3);

                            // write first 11 columns
                            swData.Write("{0};{1};{2};{3};{4};{5};{6};{7};{8};{9};{10}",
                                p.Id, course.Id, plan.Id, plan.CreationDateTime, plan.CreationUserName, plan.TotalDose.ValueAsString, plan.DosePerFraction.ValueAsString,
                                plan.NumberOfFractions, MU, MI, Math.Round(plan.PlanNormalizationValue, 1));

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
                                        struct1 = ss.Structures.FirstOrDefault(x => x.Id.ToLower() == myDiffStrucNames.ToLower());
                                        if (struct1 != null) // does the stucture exist?
                                        {
                                            if (!struct1.IsEmpty) // Is it empty?
                                            {
                                                foundOneStruct = true;
                                                DVHData dvh = plan.GetDVHCumulativeData(struct1, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);

                                                swLogFile.WriteLine("    {0} found", myDiffStrucNames); // verbose
                                                if (verbose > 0)
                                                    Console.WriteLine("    {0} found", myDiffStrucNames);
                                                foreach (string dataToGet in lineElements.Skip(1)) // loop on index
                                                {
                                                    if (verbose > 5)
                                                        Console.WriteLine(" Gimme the {0} for {1}\r\n", dataToGet, struct1.Id);

                                                    double thisValueImLookingFor = -99.999;

                                                    thisValueImLookingFor = gimmeThatBro(dataToGet, struct1, plan, dvh);

                                                    if (thisValueImLookingFor != -1.0)
                                                    {
                                                        swLogFile.WriteLine("  {0} for {1} is {2:0.00}", dataToGet, struct1.Id, thisValueImLookingFor);
                                                        swData.Write(";{0:0.00}", thisValueImLookingFor);
                                                    }
                                                    else
                                                    {
                                                        swData.Write(";"); // if data impossible to extract
                                                    }
                                                    //swData.Write(",{0:0.00}", thisValueImLookingFor);
                                                }
                                            }
                                        }
                                    }
                                }

                                if (foundOneStruct == false)
                                {
                                    Console.WriteLine("    {0} not found ********", myFirstName[0]);
                                    swLogFile.WriteLine("    {0} not found ********", myFirstName[0]);
                                    foreach (string skippedData in lineElements.Skip(1))
                                        swData.Write(";");
                                    //swData.Write(",");
                                }
                            }
                            swData.WriteLine(";");

                        }
                        //swData.Write("\r\n");

                        #endregion
                        #region VERBOSE
                        if (verbose > 5)
                        {
                            Console.WriteLine("Data hunted"); // verbose
                            Console.ReadLine();
                        }
                        #endregion


                    } //end of plan loop
                    #endregion
                    #region LOOP EVERY PLAN SUM
                    if (exploreSumPlan)
                        foreach (PlanSum plan in course.PlanSums) // loop on the plans
                        {
                            keepThisPlan = 1;
                            totalNumberOfPlans++;
                            numberOfPlansForThisPatient++;

                            if (keepThisCourse == 0)
                                keepThisPlan = 0;
                            #region VERBOSE
                            if (verbose > 5)
                            {
                                Console.WriteLine("Inspect Course: {0} Plan: {1}", course.Id, plan.Id); // verbose
                                Console.ReadLine();
                            }
                            Console.WriteLine("  Plan: {0} (course: {1})", plan.Id, course.Id); // Verbose      
                            swLogFile.WriteLine("  Plan: {0}  (course: {1})", plan.Id, course.Id); // Verbose      

                            #endregion



                            #region TEST THE PLAN

                            #region TEST IF THE PLAN HAS A NAME

                            if (keepThisPlan == 1)
                            {
                                if (keepNamedPlan == false) // dont keep  plans with a name
                                {
                                    if (plan.Name != "")  // if  name exist
                                    {
                                        keepThisPlan = keepThisPlan * 0;
                                        Console.WriteLine("         refused: THE PLAN HAS A NAME");
                                        swLogFile.WriteLine("         refused: THE PLAN HAS A NAME ");
                                    }
                                }
                                if (keepUnamedPlan == false) // dont keep plans with no name
                                {
                                    if (plan.Name == "")  // if  no name 
                                    {
                                        keepThisPlan = keepThisPlan * 0;
                                        Console.WriteLine("         refused: THE PLAN HAS NO NAME");
                                        swLogFile.WriteLine("         refused: THE PLAN HAS NO NAME ");
                                    }
                                }
                            }
                            #endregion

                            #region TEST IF PLAN CONTAINS OR DOES NOT CONTAIN SOME CHOSEN STRINGS
                            if (keepThisPlan == 1) // TEST ALL STINGS TO KEEP PLANS
                                if (keepIfPlanNameContainAstring)
                                {
                                    keepThisPlan = 0;
                                    for (int i = 2; i < stringToContainToBeKept.Length; i++)
                                    {
                                        if (plan.Id.Contains(stringToContainToBeKept[i]))
                                            keepThisPlan = 1;
                                    }
                                    if (keepThisPlan == 0)
                                    {
                                        Console.WriteLine("         refused: plan ID ({0}) does not contain one of the required strings", plan.Id);
                                        swLogFile.WriteLine("         refused: plan ID ({0}) does not contain one of the required strings", plan.Id);
                                    }
                                }

                            if (keepThisPlan == 1)  // TEST ALL STINGS TO EXCLUDE PLANS
                                if (excludeIfPlannedNameContainAString)
                                {
                                    for (int i = 2; i < stringToContainToBeExcluded.Length; i++)
                                    {
                                        if (plan.Id.Contains(stringToContainToBeExcluded[i]))
                                        {
                                            keepThisPlan = keepThisPlan * 0;
                                            Console.WriteLine("         refused: plan ID ({0}) does contain the strings {1}", plan.Id, stringToContainToBeExcluded[i]);
                                            swLogFile.WriteLine("         refused: plan ID ({0}) does contain the strings {1}", plan.Id, stringToContainToBeExcluded[i]);
                                        }
                                    }
                                }

                            #endregion

                            #endregion
                            #region VERBOSE
                            if (verbose > 5)
                            {
                                Console.WriteLine("Plan tested"); // verbose
                                Console.ReadLine();
                            }
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

                                #region GET GENERAL VALUES FOR THE PLAN SUM: MU, MI, TOTAL DOSE
                                double MU = 0;
                                int nTotalFractionOfSum = 0;
                                double totalDoseOfSum = 0;
                                double dosePerFractionOfSum = 0;

                                foreach (PlanSetup IndividualPlan in plan.PlanSetups)
                                {
                                    foreach (Beam beam in IndividualPlan.Beams)
                                    {
                                        if (!beam.IsSetupField)
                                            MU = MU + Math.Round(beam.Meterset.Value, 2);
                                    }
                                    nTotalFractionOfSum = nTotalFractionOfSum + IndividualPlan.NumberOfFractions.Value;
                                    totalDoseOfSum = totalDoseOfSum + IndividualPlan.TotalDose.Dose;
                                    lastTime = Convert.ToDateTime(IndividualPlan.CreationDateTime);
                                }

                                dosePerFractionOfSum = totalDoseOfSum / nTotalFractionOfSum;
                                var MI = Math.Round(MU / dosePerFractionOfSum, 3);
                                #endregion

                                #region Write first 11 columns
                                double cent = 100.00; //normalisation for plan sum is set to 100.00. Don't ask why
                                swData.Write("{0};{1};{2};{3};{4};{5:0.000};{6:0.000};{7};{8};{9};{10:0.0}",
                                    p.Id, course.Id, plan.Id, lastTime, "plan_sum", totalDoseOfSum, dosePerFractionOfSum,
                                    nTotalFractionOfSum, MU, MI, cent);
                                #endregion

                                StructureSet ss = plan.StructureSet;
                                bool foundOneStruct = false;
                                #region LOOP ON LINES OF THE FILE index.txt
                                foreach (string myString in list_struct) // loop on lines of user dose index (1 by struct)
                                {
                                    // get separated elements of a line (separator is a ,)
                                    lineElements = myString.Split(',');
                                    // get the different possible names of the structure (separate by a ;)
                                    string[] myFirstName = lineElements[0].Split(';');
                                    #region LOOP ON STRUCTURES
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

                                                    swLogFile.WriteLine("    {0} found", myDiffStrucNames); // verbose
                                                    if (verbose > 0)
                                                        Console.WriteLine("    {0} found", myDiffStrucNames);
                                                    #region LOOP ON INDEX
                                                    foreach (string dataToGet in lineElements.Skip(1)) // loop on index
                                                    {
                                                        if (verbose > 5)
                                                            Console.WriteLine(" Gimme the {0} for {1}\r\n", dataToGet, struct1.Id);

                                                        double thisValueImLookingFor = -99.999;

                                                        thisValueImLookingFor = gimmeThatBroSum(dataToGet, struct1, plan, dvh);

                                                        if (thisValueImLookingFor != -1.0)
                                                        {
                                                            swLogFile.WriteLine("  {0} for {1} is {2:0.00}", dataToGet, struct1.Id, thisValueImLookingFor);
                                                            swData.Write(";{0:0.00}", thisValueImLookingFor);
                                                        }
                                                        else
                                                        {
                                                            swData.Write(";"); // if data impossible to extract
                                                        }

                                                    }
                                                    #endregion
                                                }
                                            }
                                        }
                                    }
                                    #endregion


                                    if (foundOneStruct == false)
                                    {
                                        Console.WriteLine("    {0} not found ********", myFirstName[0]);
                                        swLogFile.WriteLine("    {0} not found ********", myFirstName[0]);
                                        foreach (string skippedData in lineElements.Skip(1))
                                            swData.Write(";");
                                        //swData.Write(",");
                                    }
                                }
                                swData.Write("\r\n");
                                #endregion
                            }
                            //swData.Write("\r\n");
                            #endregion
                            #region VERBOSE
                            if (verbose > 5)
                            {
                                Console.WriteLine("Data hunted"); // verbose
                                Console.ReadLine();
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

            stopwatch.Stop();
            if (verbose > 0)
            {
                Console.WriteLine("Number of accepted/total patients: {1}/{0} (accepted : at least one accepted plan)", nPatient, nPatientWithAnAcceptedPlan);
                Console.WriteLine("Number of excluded IDs {0} (more than once in id.txt)", nDoublons);
                Console.WriteLine("Number of accepted/total plans: {0}/{1}", numberOfAcceptedPlans, totalNumberOfPlans);
                Console.WriteLine("Please type Enter\n");
                Console.WriteLine("Execution time = {0:0.00} s", stopwatch.ElapsedMilliseconds / 1000);
                Console.ReadLine(); // Ask user to type enter to finish.

                swLogFile.WriteLine("Number of accepted/total patients: {1}/{0} (accepted : at least one accepted plan)", nPatient, nPatientWithAnAcceptedPlan);
                swLogFile.WriteLine("Number of excluded IDs {0} (more than once in id.txt)", nDoublons);
                swLogFile.WriteLine("Number of accepted/total plans: {0}/{1}", numberOfAcceptedPlans, totalNumberOfPlans);
                swLogFile.WriteLine("Execution time = {0:0.00} s", stopwatch.ElapsedMilliseconds / 1000);


            }
            #endregion

            #region CLOSE FILES
            swLogFile.Close();
            swData.Close();
            #endregion
        }
        #endregion

        #region AN EXTERNAL FUCTION USING REGEX TO GET THE DATA
        public static double gimmeThatBro(string myDataToGet, Structure myStruct, PlanSetup myPlan, DVHData dvh)
        {
            int verbose = 0;
            double checkThat = -1.0;

            if (verbose > 5)
                Console.WriteLine("--> looking for {0} for {1} in {2}", myDataToGet, myStruct.Id, myPlan.Id);

            #region GI       
            if (myDataToGet.ToUpper() == "GI")
            {
                DoseValue prescription = myPlan.TotalDose;
                DoseValue doseHalf = new DoseValue(0.5 * prescription.Dose, prescription.Unit);
                //new DoseValue(0.5 * prescription.Dose, prescription.Unit);
                Structure Body = myPlan.StructureSet.Structures.Single(s => s.DicomType == "BODY");
                double myFull = myPlan.GetVolumeAtDose(Body,prescription,VolumePresentation.AbsoluteCm3);
                double myHalf = myPlan.GetVolumeAtDose(Body, prescription, VolumePresentation.AbsoluteCm3);

                checkThat = 100*myHalf/myFull;
            }
            #endregion
            #region MAX DOSE       
            if (myDataToGet.ToUpper() == "MAX")
            {
                var myMaxDose = dvh.MaxDose;
                checkThat = myMaxDose.Dose;
            }
            #endregion
            #region MIN DOSE       
            if (myDataToGet.ToUpper() == "MIN")
            {
                var myMinDose = dvh.MinDose;
                checkThat = myMinDose.Dose;
            }
            #endregion
            #region MEDIAN DOSE
            if (myDataToGet.ToUpper() == "MEDIAN")
            {
                DoseValue myMedianDose = myPlan.GetDoseAtVolume(myStruct, 50, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                checkThat = myMedianDose.Dose;
            }
            #endregion
            #region MEAN DOSE
            if (myDataToGet.ToUpper() == "MEAN")
            {
                var myMeanDose = dvh.MeanDose;
                checkThat = myMeanDose.Dose;
            }
            #endregion
            #region VOLUME
            if (myDataToGet.ToUpper() == "VOL")
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
            #region HomogenityIndex
            if (myDataToGet.ToUpper() == "HI")
            {
                double d02 = Convert.ToDouble(myPlan.GetDoseAtVolume(myStruct, 2, VolumePresentation.Relative, DoseValuePresentation.Relative).ValueAsString);
                double d98 = Convert.ToDouble(myPlan.GetDoseAtVolume(myStruct, 98, VolumePresentation.Relative, DoseValuePresentation.Relative).ValueAsString);
                double d50 = Convert.ToDouble(myPlan.GetDoseAtVolume(myStruct, 50, VolumePresentation.Relative, DoseValuePresentation.Relative).ValueAsString);
                checkThat = Math.Round((d02 - d98) / d50, 3);
            }
            #endregion
            #region ConformityIndex
            if (myDataToGet.Substring(0, 2).ToUpper() == "CI")
            {
                //Conformity Index requres Body as input structure for dose calc and volume of target 
                double isodoseLvl = Convert.ToDouble(myDataToGet.Remove(0, 2)) / 100;
                Structure Body = myPlan.StructureSet.Structures.Where(x => x.DicomType == "EXTERNAL").Single();
                double volIsodoseLvl = myPlan.GetVolumeAtDose(Body, myPlan.TotalDose * isodoseLvl, VolumePresentation.AbsoluteCm3);
                checkThat = Math.Round(volIsodoseLvl / myStruct.Volume, 3);
            }
            #endregion
            #region PaddickConformityIndex
            if (myDataToGet.Substring(0, 2).ToUpper() == "PI")
            {
                //Conformation number requres both body and PTV as input structures. 


                double TV = myStruct.Volume;

                double isodoseLvl = Convert.ToDouble(myDataToGet.Remove(0, 2)) / 100;
                Structure Body = myPlan.StructureSet.Structures.Where(x => x.DicomType == "EXTERNAL").Single();

                double PIV = myPlan.GetVolumeAtDose(Body, myPlan.TotalDose * isodoseLvl, VolumePresentation.AbsoluteCm3);
                double TV_PIV = myPlan.GetVolumeAtDose(myStruct, myPlan.TotalDose * isodoseLvl, VolumePresentation.AbsoluteCm3);
                //Console.WriteLine("ttt {0} {1} {2}  ", TV_PIV, TV, PIV);
                checkThat = Math.Round((TV_PIV * TV_PIV) / (TV * PIV), 3);

            }
            #endregion
            /* DEPRECATED
            #region GradientIndex
            if (myDataToGet.Substring(0, 2).ToUpper() == "GI")
            {
                double TV = myStruct.Volume, isodoseLvl = 1;
                if (!myPlan.StructureSet.Structures.Any(x => x.DicomType.Substring(0, 3) == "ITV"))
                {
                    isodoseLvl = 0.833333333; //  Dose ITV/Dose PTV in stereo pulmonary case 
                }
                double v100 = 0.0;
                double v50 = 0.0;
                Structure Body = myPlan.StructureSet.Structures.Where(x => x.DicomType == "EXTERNAL").Single();
                v50 = myPlan.GetVolumeAtDose(Body, myPlan.TotalDose * isodoseLvl * 0.5, VolumePresentation.AbsoluteCm3);
                v100 = myPlan.GetVolumeAtDose(Body, myPlan.TotalDose * isodoseLvl, VolumePresentation.AbsoluteCm3);
                checkThat = Math.Round((v50 / v100), 2);
            }
            */
            #endregion
            #region RCI
            if (myDataToGet.Substring(0, 3).ToUpper() == "RCI")
            {
                if (myStruct.Id.Substring(0, 3).ToUpper() == "PTV")
                {
                    double TV = myStruct.Volume, isodoseLvl = 1;
                    if (!myPlan.StructureSet.Structures.Any(x => x.DicomType.Substring(0, 3) == "ITV"))
                    {
                        isodoseLvl = 0.833333333; //  Dose ITV/Dose PTV in stereo pulmonary case 
                    }

                    double volTIP = myPlan.GetVolumeAtDose(myStruct, myPlan.TotalDose * isodoseLvl, VolumePresentation.AbsoluteCm3);
                    checkThat = Math.Round(volTIP / TV, 3);
                }
            }
            #endregion
            if (Double.IsNaN(checkThat))
                checkThat = -1.0;
            if (checkThat == -1.0)
                Console.WriteLine("Impossible to obtain {0} for {1} in {2} ", myDataToGet, myStruct.Id, myPlan.Id);
            return (checkThat);
        }
        #endregion
        #region AN EXTERNAL FUCTION USING REGEX TO GET THE DATA FOR SUMPLAN
        public static double gimmeThatBroSum(string myDataToGet, Structure myStruct, PlanSum myPlan, DVHData dvh)
        {
            int verbose = 0;
            double checkThat = -1.0;
            if (verbose > 5)
                Console.WriteLine("--> looking for {0} for {1} in {2}", myDataToGet, myStruct.Id, myPlan.Id);
            #region MAX DOSE       
            if (myDataToGet.ToUpper() == "MAX")
            {

                var myMaxDose = dvh.MaxDose;
                checkThat = myMaxDose.Dose;
            }
            #endregion
            #region MIN DOSE       
            if (myDataToGet.ToUpper() == "MIN")
            {
                var myMinDose = dvh.MinDose;
                checkThat = myMinDose.Dose;
            }
            #endregion
            #region MEDIAN DOSE
            if (myDataToGet.ToUpper() == "MEDIAN")
            {
                DoseValue myMedianDose = myPlan.GetDoseAtVolume(myStruct, 50, VolumePresentation.Relative, DoseValuePresentation.Absolute);
                checkThat = myMedianDose.Dose;
            }
            #endregion
            #region MEAN DOSE
            if (myDataToGet.ToUpper() == "MEAN")
            {
                var myMeanDose = dvh.MeanDose;
                checkThat = myMeanDose.Dose;
            }
            #endregion
            #region HomogenityIndex
            if (myDataToGet.ToUpper() == "HI")
            {
                double d02 = 0.0;
                double d98 = 0.0;
                double d50 = 0.0;
                // =  Convert.ToDouble(
                d02 = myPlan.GetDoseAtVolume(myStruct, 2, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
                d50 = myPlan.GetDoseAtVolume(myStruct, 50, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
                d98 = myPlan.GetDoseAtVolume(myStruct, 98, VolumePresentation.Relative, DoseValuePresentation.Absolute).Dose;
                checkThat = Math.Round((d02 - d98) / d50, 3);
            }
            #endregion
            #region ConformityIndex
            if (myDataToGet.Substring(0, 2).ToUpper() == "CI")
            {
                //Conformity Index requres Body as input structure for dose calc and volume of target 
                double isodoseLvl = Convert.ToDouble(myDataToGet.Remove(0, 2)) / 100;
                Structure Body = myPlan.StructureSet.Structures.Where(x => x.DicomType == "EXTERNAL").Single();

                double totalDoseOfSum = 0;
                foreach (PlanSetup IndividualPlan in myPlan.PlanSetups)
                {
                    totalDoseOfSum = totalDoseOfSum + IndividualPlan.TotalDose.Dose;
                }
                totalDoseOfSum = totalDoseOfSum * isodoseLvl;
                DoseValue myDose = new DoseValue(totalDoseOfSum, DoseValue.DoseUnit.Gy);

                double volIsodoseLvl = myPlan.GetVolumeAtDose(Body, myDose, VolumePresentation.AbsoluteCm3);
                checkThat = Math.Round(volIsodoseLvl / myStruct.Volume, 3);
            }
            #endregion
            #region PaddickConformityIndex
            if (myDataToGet.Substring(0, 2).ToUpper() == "PI")
            {
                double TV = myStruct.Volume;
                double isodoseLvl = Convert.ToDouble(myDataToGet.Remove(0, 2)) / 100;
                Structure Body = myPlan.StructureSet.Structures.Where(x => x.DicomType == "EXTERNAL").Single();
                double totalDoseOfSum = 0;
                foreach (PlanSetup IndividualPlan in myPlan.PlanSetups)
                {
                    totalDoseOfSum = totalDoseOfSum + IndividualPlan.TotalDose.Dose;
                }
                totalDoseOfSum = totalDoseOfSum * isodoseLvl;
                DoseValue myDose = new DoseValue(totalDoseOfSum, DoseValue.DoseUnit.Gy);
                double PIV = myPlan.GetVolumeAtDose(Body, myDose, VolumePresentation.AbsoluteCm3);
                double TV_PIV = myPlan.GetVolumeAtDose(myStruct, myDose, VolumePresentation.AbsoluteCm3);
                //Console.WriteLine("ttt {0} {1} {2}  ", TV_PIV, TV, PIV);
                checkThat = Math.Round((TV_PIV * TV_PIV) / (TV * PIV), 3);

            }
            #endregion

            #region VOLUME
            if (myDataToGet.ToUpper() == "VOL")
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

            if (Double.IsNaN(checkThat))
                checkThat = -1.0;
            if (checkThat == -1.0)
                Console.WriteLine("Impossible to obtain {0} for {1} in {2} ", myDataToGet, myStruct.Id, myPlan.Id);
            return (checkThat);
        }
        #endregion
    }
}