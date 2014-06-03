#region Copyright (c) 2014 Two10 degrees
//
// (C) Copyright 2014 Two10 degrees
//      All rights reserved.
//
// This software is provided "as is" without warranty of any kind,
// express or implied, including but not limited to warranties as to
// quality and fitness for a particular purpose. Active Web Solutions Ltd
// does not support the Software, nor does it warrant that the Software
// will meet your requirements or that the operation of the Software will
// be uninterrupted or error free or that any defects will be
// corrected. Nothing in this statement is intended to limit or exclude
// any liability for personal injury or death caused by the negligence of
// Active Web Solutions Ltd, its employees, contractors or agents.
//
#endregion

namespace Two10.AzureFileDrive
{
    using Dokan;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.File;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.Caching;


    public class AzureOperations : DokanOperations
    {
        CloudFileClient client;
        MemoryCache streamCache = MemoryCache.Default;
        MemoryCache blobCache = MemoryCache.Default;
        MemoryCache miscCache = MemoryCache.Default;

        ConcurrentDictionary<string, CloudFileStream> blobsWriting = new ConcurrentDictionary<string, CloudFileStream>();

        Dictionary<string, string> locks = new Dictionary<string, string>();
        CloudFileShare share;
        CloudFileDirectory root;

        public AzureOperations(string connectionString, string shareName)
        {
            var account = CloudStorageAccount.Parse(connectionString);
            client = account.CreateCloudFileClient();
            share = client.GetShareReference(shareName);
            share.CreateIfNotExists();
            root = share.GetRootDirectoryReference();
        }

        public int Cleanup(string filename, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("Cleanup {0}", filename));
            return 0;
        }


        public int CreateFile(string filename, FileAccess access, FileShare share, FileMode mode, FileOptions options, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("CreateFile FILENAME({0}) ACCESS({1}) SHARE({2}) MODE({3})", filename, access, share, mode));

            if (mode == FileMode.Create || mode == FileMode.OpenOrCreate || mode == FileMode.CreateNew)
            {
                // we want to write a file
                var fileRef = Extensions.GetFileReference(root, filename.ToFileString());
                fileRef.Create(0);
                return 0;
            }

            if (share == FileShare.Delete)
            {
                return DeleteFile(filename, info);
            }

            if (GetFileInformation(filename, new FileInformation(), new DokanFileInfo(0)) == 0)
            {
                return 0;
            }
            else
            {
                return -DokanNet.ERROR_FILE_NOT_FOUND;
            }
        }

        public int OpenDirectory(string filename, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("OpenDirectory {0}", filename));
            return 0;
        }

        public int CreateDirectory(string filename, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("CreateDirectory {0}", filename));
            var dirRef = Extensions.GetDirectoryReference(root, filename.ToFileString());
            dirRef.CreateIfNotExists();
            return 0;
        }

        public int CloseFile(string filename, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("CloseFile {0}", filename));
            return 0;
        }

        public int ReadFile(string filename, byte[] buffer, ref uint readBytes, long offset, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("ReadFile {0}", filename));
            var fileRef = Extensions.GetFileReference(root, filename.ToFileString());
            readBytes = (uint)buffer.Length;
            readBytes = (uint)fileRef.DownloadRangeToByteArray(buffer, 0, offset, readBytes);
            return 0;
        }

        public int WriteFile(string filename, byte[] buffer, ref uint writtenBytes, long offset, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("WriteFile {0}", filename));
            var fileRef = Extensions.GetFileReference(root, filename.ToFileString());
            fileRef.UploadFromByteArray(buffer, 0, buffer.Length);
            writtenBytes = (uint)buffer.Length;
            return 0;
        }

        public int FlushFileBuffers(string filename, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("FlushFileBuffers {0}", filename));
            return 0;
        }

        public int GetFileInformation(string filename, FileInformation fileinfo, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("GetFileInformation {0}", filename));
            if (filename == @"\")
            {
                fileinfo.Attributes = FileAttributes.Directory;
                fileinfo.LastAccessTime = DateTime.Now;
                fileinfo.LastWriteTime = DateTime.Now;
                fileinfo.CreationTime = DateTime.Now;
                return 0;
            }
            var fileRef = Extensions.GetFileReference(root, filename.ToFileString());
            if (fileRef.Exists())
            {
                fileinfo.Length = fileRef.Properties.Length;
                fileinfo.LastAccessTime = fileRef.Properties.LastModified.Value.LocalDateTime;
                fileinfo.LastWriteTime = fileRef.Properties.LastModified.Value.LocalDateTime;
                fileinfo.CreationTime = fileRef.Properties.LastModified.Value.LocalDateTime;
                fileinfo.Attributes = FileAttributes.Normal;
                fileinfo.FileName = fileRef.Name;
                return 0;
            }
            else
            {
                var dirRef = Extensions.GetDirectoryReference(root, filename.ToFileString());
                if (dirRef.Exists())
                {
                    fileinfo.Attributes = FileAttributes.Directory;
                    fileinfo.LastAccessTime = DateTime.Now;
                    fileinfo.LastWriteTime = DateTime.Now;
                    fileinfo.CreationTime = DateTime.Now;
                    fileinfo.FileName = filename;
                    return 0;
                }
            }
            return -DokanNet.ERROR_FILE_NOT_FOUND;
        }

        public int FindFiles(string filename, System.Collections.ArrayList files, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("FindFiles {0}", filename));
            var cloudDirectory = root;
            if (filename != @"\")
            {
                cloudDirectory = Extensions.GetDirectoryReference(root, filename.ToFileString());
            }
            var output = cloudDirectory.ListFilesAndDirectories();
            foreach (var item in output)
            {
                if (item is CloudFile)
                {
                    files.Add((item as CloudFile).ToFileInformation());
                }
                else if (item is CloudFileDirectory)
                {
                    files.Add((item as CloudFileDirectory).ToFileInformation());
                }
            }
            return 0;
        }

        public int SetFileAttributes(string filename, FileAttributes attr, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("SetFileAttributes {0}", filename));
            return 0;
        }

        public int SetFileTime(string filename, DateTime ctime, DateTime atime, DateTime mtime, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("SetFileTime {0}", filename));
            return 0;
        }

        public int DeleteFile(string filename, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("DeleteFile {0}", filename));
            var fileRef = Extensions.GetFileReference(root, filename.ToFileString());
            fileRef.DeleteIfExists();
            return 0;
        }

        public int DeleteDirectory(string filename, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("DeleteDirectory {0}", filename));
            var dirRef = root.GetDirectoryReference(filename.ToFileString());
            dirRef.DeleteIfExists();
            return 0;
        }

        public int MoveFile(string filename, string newname, bool replace, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("MoveFile {0}", filename));
            return 0;
        }

        public int SetEndOfFile(string filename, long length, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("SetEndOfFile {0}", filename));
            return 0;
        }

        public int SetAllocationSize(string filename, long length, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("SetAllocationSize {0}", filename));
            return 0;
        }

        public int LockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("LockFile {0}", filename));
            return 0;
        }

        public int UnlockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("UnlockFile {0}", filename));
            return 0;
        }

        public int GetDiskFreeSpace(ref ulong freeBytesAvailable, ref ulong totalBytes, ref ulong totalFreeBytes, DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("GetDiskFreeSpace"));
            freeBytesAvailable = 512 * 1024 * 1024;
            totalBytes = 1024 * 1024 * 1024;
            totalFreeBytes = 512 * 1024 * 1024;
            return 0;
        }

        public int Unmount(DokanFileInfo info)
        {
            Trace.WriteLine(string.Format("Unmount"));
            return 0;
        }
    }

}
