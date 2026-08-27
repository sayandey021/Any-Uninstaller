using System;

namespace Klocman
{
    public static class ScriptingFileSystemHelper
    {
        private static readonly dynamic? _fso;

        static ScriptingFileSystemHelper()
        {
            try
            {
                var type = Type.GetTypeFromProgID("Scripting.FileSystemObject");
                if (type != null)
                {
                    _fso = Activator.CreateInstance(type);
                }
            }
            catch
            {
                _fso = null;
            }
        }

        public static long? GetFolderSizeBytes(string path)
        {
            if (_fso == null || string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                var folder = _fso.GetFolder(path);
                if (folder != null)
                {
                    return Convert.ToInt64(folder.Size);
                }
            }
            catch
            {
                // Folder size calculation failed
            }

            return null;
        }
    }
}
