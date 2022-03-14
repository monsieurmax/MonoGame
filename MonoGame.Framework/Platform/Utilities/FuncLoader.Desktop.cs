using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MonoGame.Framework.Utilities
{
    internal class FuncLoader
    {
        private class Windows
        {
            [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
            public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

            [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr LoadLibraryW(string lpszLib);
        }

        private class Linux
        {
            [DllImport("libdl.so.2")]
            public static extern IntPtr dlopen(string path, int flags);

            [DllImport("libdl.so.2")]
            public static extern IntPtr dlsym(IntPtr handle, string symbol);
        }

        private class OSX
        {
            [DllImport("/usr/lib/libSystem.dylib")]
            public static extern IntPtr dlopen(string path, int flags);

            [DllImport("/usr/lib/libSystem.dylib")]
            public static extern IntPtr dlsym(IntPtr handle, string symbol);
        }

        private const int RTLD_LAZY = 0x0001;

        public static IntPtr LoadLibraryExt(string libname)
        {
            Console.Out.WriteLine("Loading ext lib : {0}", libname);
            IntPtr ret;
            var assemblyLocation = Path.GetDirectoryName(typeof(FuncLoader).Assembly.Location) ?? "./";
            Console.Out.WriteLine("- assembly location  : {0}", assemblyLocation);
            Console.Out.WriteLine(">>> full Path  : {0}", Path.GetFullPath(assemblyLocation));

            Console.Out.WriteLine("- Try .NET Framework / mono locations");
            // Try .NET Framework / mono locations
            if (CurrentPlatform.OS == OS.MacOSX)
            {
                Console.Out.WriteLine("- CurrentPlatform.OS == OS.MacOSX");
                ret = LoadLibraryExtWithLogs(Path.Combine(assemblyLocation, libname));
                // Look in Frameworks for .app bundles
                if (ret == IntPtr.Zero)
                {
                    Console.Out.WriteLine("- Look in Frameworks for .app bundles");
                    ret = LoadLibraryExtWithLogs(Path.Combine(assemblyLocation, "..", "Frameworks", libname));
                }
            }
            else
            {
                Console.Out.WriteLine("- CurrentPlatform.OS != OS.MacOSX");
                if (Environment.Is64BitProcess)
                {
                    Console.Out.WriteLine("- Env is 64bit");
                    ret = LoadLibraryExtWithLogs(Path.Combine(assemblyLocation, "x64", libname));
                }
                else
                {
                    Console.Out.WriteLine("- Env is 32bit");
                    ret = LoadLibraryExtWithLogs(Path.Combine(assemblyLocation, "x86", libname));
                }
            }


            // Try .NET Core development locations
            if (ret == IntPtr.Zero)
            {
                Console.Out.WriteLine("- Try .NET Core development locations");
                ret = LoadLibraryExtWithLogs(Path.Combine(assemblyLocation, "runtimes", CurrentPlatform.Rid, "native", libname));
            }

            // Try current folder (.NET Core will copy it there after publish)
            if (ret == IntPtr.Zero)
            {
                Console.Out.WriteLine("-  Try current folder (.NET Core will copy it there after publish)");
                ret = LoadLibraryExtWithLogs(Path.Combine(assemblyLocation, libname));
            }

            // Try loading system library
            if (ret == IntPtr.Zero)
            {
                Console.Out.WriteLine("- Try loading system library");
                ret = LoadLibraryExtWithLogs(libname);
            }

            // Welp, all failed, PANIC!!!
            if (ret == IntPtr.Zero)
                throw new Exception("Failed to load library: " + libname);

            return ret;
        }

        private static IntPtr LoadLibraryExtWithLogs(string path)
        {
            IntPtr ret = LoadLibrary(path);
            Console.Out.WriteLine("- LoadLibrary at {0} : result={1}", path, ret);
            Console.Out.WriteLine(">>> Exists {1} at full Path  : {0}", path, File.Exists(path));

            return ret;
        }

        public static IntPtr LoadLibrary(string libname)
        {
            if (CurrentPlatform.OS == OS.Windows)
                return Windows.LoadLibraryW(libname);

            if (CurrentPlatform.OS == OS.MacOSX)
                return OSX.dlopen(libname, RTLD_LAZY);

            return Linux.dlopen(libname, RTLD_LAZY);
        }

        public static T LoadFunction<T>(IntPtr library, string function, bool throwIfNotFound = false)
        {
            var ret = IntPtr.Zero;

            if (CurrentPlatform.OS == OS.Windows)
                ret = Windows.GetProcAddress(library, function);
            else if (CurrentPlatform.OS == OS.MacOSX)
                ret = OSX.dlsym(library, function);
            else
                ret = Linux.dlsym(library, function);

            if (ret == IntPtr.Zero)
            {
                if (throwIfNotFound)
                    throw new EntryPointNotFoundException(function);

                return default(T);
            }

#if NETSTANDARD
            return Marshal.GetDelegateForFunctionPointer<T>(ret);
#else
            return (T)(object)Marshal.GetDelegateForFunctionPointer(ret, typeof(T));
#endif
        }
    }
}