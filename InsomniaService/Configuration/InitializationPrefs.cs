using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Configuration
{
    public class InitializationPrefs(string iniFilePath)
    {
        public Section this[string name]
        {
            get
            {
                return new Section(iniFilePath, name);
            }
        }

        public class Section(string iniFilePath, string name)
        {
            public string? this[string key]
            {
                get
                {
                    var stringBuilder = new StringBuilder(1024);

                    var charCount = GetPrivateProfileString(name, key, "", stringBuilder, stringBuilder.Capacity, iniFilePath);

                    if (charCount == 0)
                        return null;

                    return stringBuilder.ToString();
                }

                set
                {
                    if (value == null)
                        return;

                    if (WritePrivateProfileString(name, key, value, iniFilePath) == 0)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder value, int maxSize, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int WritePrivateProfileString(string section, string key, string value, string filePath);
    }
}
