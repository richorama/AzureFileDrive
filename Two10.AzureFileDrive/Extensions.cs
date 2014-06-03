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
    using Microsoft.WindowsAzure.Storage.File;
    using System;
    using System.IO;
    using System.Linq;

    public static class Extensions
    {
        public static string ToFileString(this string value)
        {
            if (value == "") return value;
            if (value[0] == '\\') return value.Remove(0, 1);
            return value;
        }

        public static FileInformation ToFileInformation(this CloudFileDirectory directory)
        {
            return new FileInformation
            {
                Attributes = FileAttributes.Directory,
                Length = 0,
                LastAccessTime = DateTime.Now,
                LastWriteTime = DateTime.Now,
                CreationTime = DateTime.Now,
                FileName = directory.Name,
            };
        }

        public static FileInformation ToFileInformation(this CloudFile cloudFile)
        {
            cloudFile.FetchAttributes();
            return new FileInformation
            {
                Length = cloudFile.Properties.Length,
                LastAccessTime = cloudFile.Properties.LastModified.Value.LocalDateTime,
                LastWriteTime = cloudFile.Properties.LastModified.Value.LocalDateTime,
                CreationTime = cloudFile.Properties.LastModified.Value.LocalDateTime,
                Attributes = FileAttributes.Normal,
                FileName = cloudFile.Name,
            };
        }

        public static CloudFileDirectory GetDirectoryReference(CloudFileDirectory parent, string path)
        {
            if (path.Contains(@"\"))
            {
                var paths = path.Split('\\');
                return GetDirectoryReference(parent.GetDirectoryReference(paths.First()), string.Join(@"\", paths.Skip(1)));
            }
            else
            {
                return parent.GetDirectoryReference(path);
            }
        }

        public static CloudFile GetFileReference(CloudFileDirectory parent, string path)
        {
            var filename = Path.GetFileName(path);
            var fullPath = Path.GetDirectoryName(path);
            if (fullPath == string.Empty)
            {
                return parent.GetFileReference(filename);
            }
            var dirReference = GetDirectoryReference(parent, fullPath);
            return dirReference.GetFileReference(filename);
        }

    }
}
