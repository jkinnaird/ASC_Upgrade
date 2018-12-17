using System;
using System.IO;
using System.IO.Compression;
using System.ServiceProcess;
using System.Threading;

namespace ASC_Upgrade
{
    static class Install
    {
        /// <summary>
        /// Unzips a zip file to a designated directory
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        /// <param name="clean"></param>
        /// <returns></returns>
        public static bool unzip(string source, string dest, bool clean)
        {
            Program.log("Unzipping " + source + " to " + dest);
            try
            {
                //wipe old directory
                if (clean) { Clean(dest, true); }    //if reverting to backup, a clean install is needed to avoid having to manually overwrite all files
                else { Clean(dest, false); }         //else the config file and java need to stay due to payload size limitations

                //extract new ASC directory
                try
                {
                    Program.log("Extracting new files", true);
                    ZipFile.ExtractToDirectory(source, dest);                                               //extract the source zip to the destination directory
                    if (!Directory.Exists("D:\\ASC\\logs")) { Directory.CreateDirectory("D:\\ASC\\logs"); } //create the logs directory, as unzipping does not create empty directories
                    return true;
                }
                catch (Exception e) { Program.log(e.ToString(), true); return false; }
            }
            catch(Exception e) { Program.log(e.ToString(), true); return false; }
        }

        /// <summary>
        /// Unpacks an embedded file to a designated directory
        /// </summary>
        /// <param name="dest"></param>
        /// <returns></returns>
        public static bool unpack(string dest)
        {
            Program.log("Unpacking the embedded ASC file to " + dest);
            try
            {
                if (File.Exists(dest)) { File.Delete(dest); } //if the new zip is already on the drive (from a previous install?), replace it
                File.WriteAllBytes(dest, Properties.Resources.ASC);//write the embedded zip to the destination file
                if (File.Exists(dest)) { return true; }      //check if zip exists in file system
                else return false;
            }
            catch (Exception e) { Program.log(e.ToString(), true); return false; }
        }

        /// <summary>
        /// Verifies a designated service does not crash for at least 2 minutes
        /// </summary>
        /// <param name="service_name"></param>
        /// <returns></returns>
        public static bool verify(string service_name)
        {
            Program.log("Checking " + service_name + " for stability...");
            bool running = true;                                        //variable to hold if service is running or not
            DateTime started = DateTime.Now;                            //get start time
            ServiceController service = new ServiceController(service_name);   //create service object

            while ((DateTime.Now - started) < TimeSpan.FromMinutes(2))  //while the time elapsed is not greater than 2 minutes
            {
                service.Refresh();                                      //refresh service data, status is not refreshed when called
                if (service.Status != ServiceControllerStatus.Running)   //if the service is not running
                {
                    Program.log("Service state is " + service.Status, true);
                    running = false;                                    //set the monitor variable to false
                    Thread.Sleep(5000);                                 //wait 5 seconds to ensure the service is fully stopped before performing actions with it
                    break;                                              //break the loop
                }
                else { Program.log("Service state is " + service.Status, true); }
                Thread.Sleep(5000);                                     //sleep between checks so as to not stress the CPU
            }
            return running;
        }
        
        /// <summary>
        /// Cleans a directory to prepare for extracting to that directory without worrying about overwrite exceptions
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="clean"></param>
        /// <returns></returns>
        public static bool Clean(string dir, bool clean)
        {
            Program.log("Cleaning " + dir);
            //wipe old ASC directory except for config.ini
            try
            {
                if (Directory.Exists(dir))                                                  //if the designated directory exists
                {
                    if (clean)                                                              //if a total wipe was designated (restore from backup path)
                    {
                        Program.log("Full wipe of " + dir, true);
                        if (Directory.Exists(dir)) { Directory.Delete(dir, true); }         //delete the entire directory recursively
                        if (!Directory.Exists(dir)) { return true; }
                        else return false;
                    }
                    else                                                                    //if a clean wipe is not designated (upgrade path)
                    {
                        Program.log("Partial wipe of " + dir, true);
                        foreach (string entry in Directory.GetFileSystemEntries(dir, "*", SearchOption.AllDirectories)) //search for all files
                        {
                            if (File.Exists(entry) && !entry.Contains("config.ini") && !entry.Contains("java")) //for each file that is not the config file or in the java directory
                            {
                                File.Delete(entry);                                         //delete file
                            }
                        }
                        foreach (string entry in Directory.GetFileSystemEntries(dir, "*"))  //for all root directories
                        {
                            if (Directory.Exists(entry) && !entry.Contains("java"))         //if the directory is not a java directory
                            {
                                Directory.Delete(entry, true);                              //delete the directory recursively
                            }
                        }
                        DirectoryInfo directory = new DirectoryInfo(dir);
                        if (directory.GetFileSystemInfos().Length == 2) { return true; }
                        else return false;
                    }
                }
                else { Program.log("Existing ASC directory not found", true); return true; }
            }
            catch (Exception e) { Program.log(e.ToString(), true); return false; }
        }
    }
}
