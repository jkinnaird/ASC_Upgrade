using System;
using System.IO;
using System.IO.Compression;

namespace ASC_Upgrade
{
    static class Backup
    {
        /// <summary>
        /// Verifies a backup directory against a source directory 
        /// </summary>
        /// <param name="source_dir"></param>
        /// <param name="dest_file"></param>
        /// <returns></returns>
        public static bool verify_backup(string source_dir, string dest_file)
        {
            Program.log("Verifying " + dest_file + " against " + source_dir);
            if (File.Exists(dest_file) && Directory.Exists(source_dir))
            {
                int entries = 0;   //number of entries in the zip
                int files = 0;     //number of files in the directory

                using (ZipArchive archive = ZipFile.OpenRead(dest_file))   //open the backup file
                {
                    entries = archive.Entries.Count;
                    Program.log(archive.Entries.Count + " entries in backup zip", true);
                }

                foreach (string entry in Directory.GetFileSystemEntries(source_dir, "*", SearchOption.AllDirectories)) //search the directory for all filesystem objects
                {
                    //zip archives count empty directories as entries, so therefore I need to count all empty directories to match
                    if (Directory.Exists(entry))                            //check if entry is a directory
                    {
                        DirectoryInfo temp_dir = new DirectoryInfo(entry);
                        if (temp_dir.GetFileSystemInfos().Length == 0)       //if there are no files in this directory
                        {
                            files++;                                        //count as an entry
                        }
                    }
                    if (File.Exists(entry))         //check to see if the entry is a file
                    {
                        files++;                    //add 1 to the files counter
                    }
                }
                Program.log(files + " entries in source directory", true);

                if (files == entries) { return true; }
                else { Program.log(files + " - " + entries, true); return false; }
            }
            else return false;
        }

        /// <summary>
        /// Backs up a directory to a designated zip file
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        /// <returns></returns>
        public static bool backup(string source, string dest)
        {
            Program.log("Backing up " + source + " to " + dest);
            if (Directory.Exists(source))
            {
                //------------------------------------------------------------------
                //                  backup current ASC and verify backup
                //------------------------------------------------------------------
                try
                {
                    if (File.Exists(dest)) { File.Delete(dest); }        //delete any existing backup zip (should be highly unlikely unless run more than once)
                    ZipFile.CreateFromDirectory(source, dest);           //create a new backup from the current directory
                }
                catch (Exception e) { Program.log(e.ToString()); }
            }
            else { Program.log(source + " not found.", true); return false; }
            if (File.Exists(dest)) { return true; }
            else { Program.log(dest + " not found.", true); return false; }
        }
    }
}
