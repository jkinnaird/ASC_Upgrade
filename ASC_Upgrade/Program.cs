using System;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace ASC_Upgrade
{
    class Program
    {
        /// <summary>
        /// If run from a console window, this function will attach the console window so output will be displayed
        /// </summary>
        /// <param name="dwProcessId"></param>
        /// <returns>bool success</returns>
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        //verbose mode
        static bool verbose = false;

        /// <summary>
        /// Converts Dictionary to Json and Saved to D:\Temp and uploads to the feeds server
        /// </summary>
        /// <param name="report"></param>
        /// <returns>bool success</returns>
        static bool Post(Dictionary<string, bool> report)
        {
            string response;
            string json_file_name = "D:\\Temp\\" + Environment.MachineName + "_ASC.json";
            
            //get which method called this method to determine if it is an upgrade or not, set the filename accordingly
            //file name is the player name + install type + .json
            StackTrace stackTrace = new StackTrace();
            string calling_method = stackTrace.GetFrame(2).GetMethod().Name.ToLower();
            if (calling_method.Contains("upgrade")) { json_file_name = "D:\\Temp\\" + Environment.MachineName + "_ASC_UPGRADE.json"; }
            else if (calling_method.Contains("install")) { json_file_name = "D:\\Temp\\" + Environment.MachineName + "_ASC_INSTALL.json"; }
            else if (calling_method.Contains("revert")) { json_file_name = "D:\\Temp\\" + Environment.MachineName + "_ASC_REVERT.json"; }

            //convert Dict to a Json
            string json = "{";                                      //start json with a {
            int entries_to_go = report.Keys.Count;                  //get number of entries in the Dict
            foreach(string key in report.Keys)                      //for each entry
            {
                string temp = "\"" + key + "\": " + report[key];    //add "key_name": key_value
                if (entries_to_go != 1) { json += temp + ", "; }    //if this is not the last entry, add a comma at the end
                else { json += temp + "}"; }                        //if this is the last entry, add a }
                entries_to_go--;                                    //subtract 1 from entries_to_go
            }

            json = json.ToLower();                                  //convert to lowercase, just in case anything uppercase got introduced somehow
            log(json);                                              //output the json
            
            string uri = "http://scalafeeds.abnetwork.com/aggregator/api/mi/";  //endpoint to send json to

            //write the json to a file first so no matter what we have a log of what happened
            if (!Directory.Exists("D:\\Temp")) { Directory.CreateDirectory("D:\\Temp"); }
            using (StreamWriter writer = new StreamWriter(json_file_name))
            {
                writer.Write(json);
            }

            //upload that file to the endpoint
            using (WebClient client = new WebClient())
            {
                byte[] response_bytes = client.UploadFile(uri, json_file_name);
                response = Encoding.ASCII.GetString(response_bytes);
            }

            //the response should contain the word successful if it was uploaded
            if (response.ToLower().Contains("successful")) { Console.WriteLine(json_file_name + " uploaded!"); return true; }
            else { Console.WriteLine(response); return false; }
        }

        /// <summary>
        /// Processes exit codes, reports data, and exits with a calculated exit code
        /// </summary>
        /// <param name="report"></param>
        /// <param name="return_override"></param>
        static void Exit(Dictionary<string, bool> report, int return_override = -50)
        {
            int return_code = return_override;
            if (!Post(report)) { return_code = -2; }

            //if override not specified
            if (return_override == -50)
            {
                return_code = 0;
                foreach(bool value in report.Values)
                {
                    if (!value) { return_code--; }
                }
            }
            log("Press enter to exit...");
            Environment.Exit(return_code);
        }

        /// <summary>
        /// Outputs a string preceded by an appropriate descriptor to seperate standard vs verbose output
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="is_verbose"></param>
        public static void log(string entry, bool is_verbose = false)
        {
            if (verbose && is_verbose) { Console.WriteLine("verb: " + entry); }
            else if (!is_verbose) { Console.WriteLine("info: " +  entry); }
            //Thread.Sleep(5000);
        }

        /// <summary>
        /// Performs actions with a designated service
        /// </summary>
        /// <param name="service_name"></param>
        /// <param name="action"></param>
        /// <example>Service_Control("ASC", "START")</example>>
        /// <example>Service_Control("ASC", "STOP")</example>>
        /// <returns></returns>
        static bool Service_control(string service_name, string action)
        {
            bool success = false;
            
            bool service_exists = false;                                        //if service exists

            foreach (var serv in ServiceController.GetServices())
            {
                if (serv.ServiceName == service_name) { service_exists = true; break; } //loop through all services to see if one matches our service_name
            }

            if (service_exists)
            {
                try
                {
                    ServiceController service = new ServiceController(service_name);
                    TimeSpan timeout = new TimeSpan(0, 0, 15); // 5seconds

                    if (action == "STOP")
                    {
                        log("Stopping " + service_name + "...");
                        if (service.Status != ServiceControllerStatus.Stopped)
                        {
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                            success = true;
                        }
                        else success = true;
                    }
                    else if (action == "START")
                    {
                        log("Starting " + service_name + "...");
                        if (service.Status != ServiceControllerStatus.Running)
                        {
                            service.Start();
                            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                            success = true;
                        }
                    }
                }
                catch (System.ServiceProcess.TimeoutException te) { log(te.ToString(), true); success = false; }
                catch (Exception e) { log(e.ToString()); }
            }
            else { success = true; }

            return success;
        }

        /// <summary>
        /// Installs ASC for the first time
        /// </summary>
        static void install()
        {
            //currently empty due to size constraints
            //Solarwinds has a max file size of 50 MB, the standard ASC install alone is ~60 MB due primarily to Java
            Dictionary<string, bool> report = new Dictionary<string, bool>();
            report.Add("extraction", false);
            report.Add("no_previous_install", false);

            string backup_path = "D:\\ASC_AUTO_BAK.zip";
            string temp_path = "D:\\ASC_NEW.zip";       //location to extract the embedded zip to
            string dest_dir = "D:\\ASC";                //current and destination ASC directory
            string service_name = "ASC";                //name of the service to stop and start
            /*
            //if directory already exists, back it up then delete it
            if(Directory.Exists(dest_dir))
            {
                Service_control("ASC", "STOP");
                if(!Backup.backup(dest_dir, backup_path)) { Exit(report); }
                if(!Backup.verify_backup(dest_dir, backup_path)) { Exit(report); }
                else { Directory.Delete(dest_dir, true); }
            }
            else { report["no_previous_install"] = true; }

            //------------------------------------------------------------------
            //                  unpack embedded ASC.zip
            //------------------------------------------------------------------
            if (!Install.unpack(temp_path)) { Exit(report); }

            //------------------------------------------------------------------
            //                  install new version
            //------------------------------------------------------------------
            if (Install.unzip(temp_path, dest_dir, false))                                                            //if install of files succeeded
            {
                File.Delete(temp_path);                                                                             //delete temporary update zip
                report["extraction"] = true;
                if (Service_control(service_name, "START"))                                                          //start the service
                {
                    if (Install.verify(service_name)) { log("ASC upgrade successfull"); report["execution"] = true; Exit(report, return_override: 0); }//if ASC runs for 2 minutes, success
                    else { log("Service failed to keep running, reverting to previous version"); report["execution"] = false; }            //else it crashed, revert to backup
                }
                else { log("Service failed to start, reverting to previous version"); report["execution"] = false; }                       //service did not start, revert to backup
            }
            else { log("ASC install general failure"); report["extraction"] = false; File.Delete(temp_path); }                             //unknown failure, revert to backup
            log("execution: " + report["execution"]);
            */
            Exit(report);
        }

        /// <summary>
        /// upgrades ASC to the packaged version
        /// </summary>
        static void upgrade()
        {
            Dictionary<string, bool> report = new Dictionary<string, bool>();
            report.Add("backup", false);
            report.Add("extraction", false);
            report.Add("execution", false);
            report.Add("reversion", false);
            report.Add("attention", false);

            string temp_path = "D:\\ASC_NEW.zip";       //location to extract the embedded zip to
            string dest_dir = "D:\\ASC";                //current and destination ASC directory
            string service_name = "ASC";                //name of the service to stop and start
            string backup_path = "D:\\ASC_AUTO_BAK.zip";//location to store the backup zip

            //If ASC is not installed, exit without doing anything
            if(!Directory.Exists(dest_dir))
            {
                Exit(report);
            }

            try
            {
                //stop service and start the upgrade process
                if (Service_control(service_name, "STOP"))
                {
                    //------------------------------------------------------------------
                    //                  unpack embedded ASC.zip
                    //------------------------------------------------------------------
                    if (!Install.unpack(temp_path)) { Exit(report); }
                    else { report["extraction"] = true; }

                    //------------------------------------------------------------------
                    //                  backup current ASC and verify backup
                    //------------------------------------------------------------------
                    if (Directory.Exists(dest_dir))
                    {
                        if (!Backup.backup(dest_dir, backup_path)) { Exit(report); }
                        if (!Backup.verify_backup(dest_dir, backup_path)) { Exit(report); }
                        else { report["backup"] = true; }
                    }
                    else { report["backup"] = true; }

                    //------------------------------------------------------------------
                    //                  install new version
                    //------------------------------------------------------------------
                    if (Install.unzip(temp_path, dest_dir, false))                                                            //if install of files succeeded
                    {
                        File.Delete(temp_path);                                                                             //delete temporary update zip
                        report["extraction"] = true;
                        if (Service_control(service_name, "START"))                                                          //start the service
                        {
                            if (Install.verify(service_name)) { log("ASC upgrade successfull"); report["execution"] = true; Exit(report, return_override: 0); }//if ASC runs for 2 minutes, success
                            else { log("Service failed to keep running, reverting to previous version"); report["execution"] = false; }            //else it crashed, revert to backup
                        }
                        else { log("Service failed to start, reverting to previous version"); report["execution"] = false; }                       //service did not start, revert to backup
                    }
                    else { log("ASC install general failure"); report["extraction"] = false; File.Delete(temp_path); }                             //unknown failure, revert to backup

                    //------------------------------------------------------------------
                    //                  Revert back to old version
                    //------------------------------------------------------------------
                    if (Install.unzip(backup_path, dest_dir, true))                                                           //if re-installing old files succeeded
                    {
                        report["extraction"] = true;
                        if (Service_control(service_name, "START"))                                                         //start the service
                        {
                            if (Install.verify(service_name)) { log("ASC reversion successfull"); report["reversion"] = true; Exit(report); }   //if ASC runs for 2 minutes, reversion successfull
                            else { log("Service failed to keep running, immediate attention required"); report["reversion"] = false; report["attention"] = true; }//else ASC crashed, immediate attention required
                        }
                        else { log("Service failed to start, , immediate attention required"); report["reversion"] = false; report["attention"] = true; }         //service did not start, immediate attention required
                    }
                    else { log("ASC reverion general failure, immediate attention required"); report["reversion"] = false; report["attention"] = true; }          //unknown failure, immediate attention required
                }
                //exit if the service did not stop
                else { log("Not Stopped"); report["reversion"] = false; report["attention"] = true; Exit(report); }                                                     //service never stopped, no actions performed

                //exit with return code and output json if not done so already
                Exit(report);
            }
            catch(Exception e) { log(e.ToString(), true); Exit(report); }
        }

        /// <summary>
        /// reverts to the ASC version stored in D:\\ASC_AUTO_BAK.zip
        /// </summary>
        static void revert()
        {
            Dictionary<string, bool> report = new Dictionary<string, bool>();
            report.Add("backup", false);
            report.Add("extraction", false);
            report.Add("execution", false);
            report.Add("reversion", false);
            report.Add("attention", false);

            string dest_dir = "D:\\ASC";                //current and destination ASC directory
            string service_name = "ASC";                //name of the service to stop and start
            string old_backup = "D:\\ASC_AUTO_BAK.zip"; //location of the original backup
            string backup_path = "D:\\ASC_REVERT_BAK.zip";//location to store the backup zip

            try
            {
                if (File.Exists(old_backup) && Service_control(service_name, "STOP"))
                {
                    //------------------------------------------------------------------
                    //                  backup current ASC and verify backup
                    //------------------------------------------------------------------
                    if (Directory.Exists(dest_dir))
                    {
                        if (!Backup.backup(dest_dir, backup_path)) { Exit(report); }
                        if (!Backup.verify_backup(dest_dir, backup_path)) { Exit(report); }
                        else { report["backup"] = true; }
                    }
                    else { report["backup"] = true; }

                    //------------------------------------------------------------------
                    //                  Revert to old version
                    //------------------------------------------------------------------
                    if (Install.unzip(old_backup, dest_dir, true))                                                           //if install of files succeeded
                    {
                        report["extraction"] = true;
                        if (Service_control(service_name, "START"))                                                          //start the service
                        {
                            if (Install.verify(service_name)) { log("ASC upgrade successfull"); report["execution"] = true; Exit(report, return_override: 0); }//if ASC runs for 2 minutes, success
                            else { log("Service failed to keep running, reverting to previous version"); report["execution"] = false; }            //else it crashed, revert to backup
                        }
                        else { log("Service failed to start, reverting to previous version"); report["execution"] = false; }                       //service did not start, revert to backup
                    }
                    else { log("ASC install general failure"); report["extraction"] = false; }                             //unknown failure, revert to backup
                    log("execution: " + report["execution"]);

                    //------------------------------------------------------------------
                    //                  Revert back to new version
                    //------------------------------------------------------------------
                    if (Install.unzip(backup_path, dest_dir, true))                                                           //if re-installing old files succeeded
                    {
                        report["extraction"] = true;
                        if (Service_control(service_name, "START"))                                                         //start the service
                        {
                            if (Install.verify(service_name)) { log("ASC reversion successfull"); report["reversion"] = true; Exit(report); }   //if ASC runs for 2 minutes, reversion successfull
                            else { log("Service failed to keep running, immediate attention required"); report["reversion"] = false; report["attention"] = true; }//else ASC crashed, immediate attention required
                        }
                        else { log("Service failed to start, , immediate attention required"); report["reversion"] = false; report["attention"] = true; }         //service did not start, immediate attention required
                    }
                    else { log("ASC reverion general failure, immediate attention required"); report["reversion"] = false; report["attention"] = true; }          //unknown failure, immediate attention required
                }
                else { Exit(report); }

                Exit(report);
            }
            catch(Exception e) { log(e.ToString(), true); Exit(report); }
        }

        /// <summary>
        /// Reads arguments and runs the specified function, defaulting to upgrade
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            //try to attach the console window
            try { AttachConsole(-1); }
            catch { }

            //if an argument was given, figure out what action to perform
            if (args.Length > 0)
            {
                if (args.Length > 1)
                {
                    if (args[1].ToString().ToUpper().Contains("VERBOSE"))   //check for verbose logging mode
                    {
                        verbose = true;
                        Console.WriteLine("Args:");
                        foreach(var arg in args)
                        {
                            Console.WriteLine("\t-" + arg);
                        }
                    }
                }
                if (args[0].ToString().ToUpper().Contains("INSTALL"))       //if the first arg was an install flag, do a first time install
                {
                    //if (verbose) { install(); }
                    /*else { */Console.WriteLine("Function not yet implemented");// }
                }
                else if (args[0].ToString().ToUpper().Contains("UPGRADE"))  //else if it was an upgrade flag, upgrade the ASC version
                {
                    upgrade();
                }
                else if (args[0].ToString().ToUpper().Contains("REVERT"))   //revert to old version of ASC
                {
                    revert();
                }
                else if (args[0].ToString().Contains("?"))                  //write the help information
                {
                    Console.WriteLine(@"
Usage: " + System.AppDomain.CurrentDomain.FriendlyName +  @" [-UPGRADE] [-REVERT]
                                        
Options:
    -UPGRADE        Upgrades ASC to the ASC version packaged with this application
                    stores the old version under ASC_AUTO_BAK.zip
    -REVERT         Reverts to the ASC version stored under ASC_AUTO_BAK.zip
                    backs up the current ASC directory to ASC_REVERT_BAK.zip");
                }
                else
                {
                    Console.WriteLine("Invalid Argument: " + args[0]);      //else the function specified does not exist}
                }
            }
            //if no args given, default is upgrade
            else
            {
                Console.WriteLine("UPGRADE");
                upgrade();
            }
        }
    }
}
