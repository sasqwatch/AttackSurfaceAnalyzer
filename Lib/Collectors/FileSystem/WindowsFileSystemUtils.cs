﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using AttackSurfaceAnalyzer.Libs;
using AttackSurfaceAnalyzer.ObjectTypes;
using AttackSurfaceAnalyzer.Utils;
using Serilog;

namespace AttackSurfaceAnalyzer.Collectors.FileSystem
{
    public class WindowsFileSystemUtils
    {
        public static List<string> SIGNED_EXTENSIONS = new List<string> { "dll", "exe", "cab", "ocx" };

        protected internal static string GetSignatureStatus(string Path)
        {
            if (!WindowsFileSystemUtils.NeedsSignature(Path))
            {
                return "";
            }
            string sigStatus = WinTrust.VerifyEmbeddedSignature(Path);

            return sigStatus;
        }

        protected internal static bool NeedsSignature(string Path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var _p = Path.ToLower().Trim();
                foreach (var ext in SIGNED_EXTENSIONS)
                {
                    if (_p.EndsWith("." + ext))
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                return false;
            }
        }

        protected internal static List<DLLCHARACTERISTICS> GetDllCharacteristics(string Path)
        {
            if (NeedsSignature(Path))
            {
                try
                {
                    List<DLLCHARACTERISTICS> l = new List<DLLCHARACTERISTICS>();
                    var peHeader1 = new PeNet.PeFile(Path);
                    ushort characteristics = peHeader1.ImageNtHeaders.OptionalHeader.DllCharacteristics;
                    foreach (DLLCHARACTERISTICS characteristic in Enum.GetValues(typeof(DLLCHARACTERISTICS)))
                    {
                        if (((ushort)characteristic & characteristics) == (ushort)characteristic)
                        {
                            l.Add(characteristic);
                        }
                    }
                    return l;
                }
                // Catches a case where the line establising the PeFile fails with Index outside bounds of the array.
                catch (IndexOutOfRangeException)
                {
                    Log.Verbose("Failed to get PE headers for {0}", Path);
                }
                catch (Exception e)
                {
                    Log.Debug("{0}:{1}", e.GetType().ToString(), e.Message);
                    Log.Debug(e.StackTrace);
                }
            }
            
            return new List<DLLCHARACTERISTICS>();
        }

        protected internal static string GetFilePermissions(FileSystemInfo fileInfo)
        {
            FileSystemSecurity fileSecurity = null;
            var filename = fileInfo.FullName;
            if (filename.Length >= 260 && !filename.StartsWith(@"\\?\"))
            {
                filename = string.Format(@"\\?\{0}", filename);
            }

            if (fileInfo is FileInfo)
            {
                try
                {
                    fileSecurity = new FileSecurity(filename, AccessControlSections.All);
                }
                catch (UnauthorizedAccessException)
                {
                    Log.Verbose(Strings.Get("Err_AccessControl"), fileInfo.FullName);
                }
                catch (InvalidOperationException)
                {
                    Log.Verbose("Invalid operation exception {0}.", fileInfo.FullName);
                }
                catch (Exception e)
                {
                    Log.Debug("Exception {0}: {1}", e.GetType().ToString(), fileInfo.FullName);
                    Log.Debug(e.StackTrace);
                }
            }
            else if (fileInfo is DirectoryInfo)
            {
                try
                {
                    fileSecurity = new DirectorySecurity(filename, AccessControlSections.All);
                }
                catch (UnauthorizedAccessException)
                {
                    Log.Verbose(Strings.Get("Err_AccessControl"), fileInfo.FullName);
                }
                catch (InvalidOperationException)
                {
                    Log.Verbose("Invalid operation exception {0}.", fileInfo.FullName);
                }
                catch (Exception e)
                {
                    Log.Debug("Exception {0}: {1}", e.GetType().ToString(), fileInfo.FullName);
                    Log.Debug(e.StackTrace);
                }
            }
            else
            {
                return null;
            }
            if (fileSecurity != null)
                return fileSecurity.GetSecurityDescriptorSddlForm(AccessControlSections.All);
            else
                return "";
        }
    }
}