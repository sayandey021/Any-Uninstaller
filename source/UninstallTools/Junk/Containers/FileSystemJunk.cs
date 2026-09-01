/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.IO;
using Klocman.Tools;

namespace UninstallTools.Junk.Containers
{
    public class FileSystemJunk : JunkResultBase
    {
        public FileSystemJunk(FileSystemInfo path, ApplicationUninstallerEntry application, IJunkCreator source) : base(application, source)
        {
            Path = path;
        }

        public FileSystemInfo Path { get; }

        public override void Backup(string backupDirectory)
        {
            // Items are deleted to the recycle bin
        }

        public override void Delete()
        {
            if (Path is DirectoryInfo dir)
            {
                if (dir.Exists)
                {
                    RemoveReadOnlyAttributes(dir);
                    dir.Delete(true);
                }
            }
            else if (Path is FileInfo file)
            {
                if (file.Exists)
                {
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                }
            }
            else
            {
                throw new NotImplementedException("Unknown FileSystemInfo implementation");
            }
        }

        private static void RemoveReadOnlyAttributes(DirectoryInfo directory)
        {
            try
            {
                directory.Attributes = FileAttributes.Normal;
                foreach (var f in directory.GetFiles("*", SearchOption.AllDirectories))
                {
                    try { f.Attributes = FileAttributes.Normal; } catch { }
                }
            }
            catch { }
        }

        public override string GetDisplayName()
        {
            return Path.FullName;
        }

        public override void Open()
        {
            if (Path.Exists)
                WindowsTools.OpenExplorerFocusedOnObject(Path.FullName);
            else
                throw new FileNotFoundException(null, Path.FullName);
        }
    }
}