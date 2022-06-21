﻿/*
    Kryptor: A simple, modern, and secure encryption and signing tool.
    Copyright (C) 2020-2022 Samuel Lucas

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program. If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.IO;
using System.Collections.Generic;

namespace Kryptor;

public static class FilePathValidation
{
    private static readonly char[] IllegalFileNameChars = {
        '\"', '<', '>', '|', '\0',
        (char) 1, (char) 2, (char) 3, (char) 4, (char) 5, (char) 6, (char) 7, (char) 8, (char) 9, (char) 10,
        (char) 11, (char) 12, (char) 13, (char) 14, (char) 15, (char) 16, (char) 17, (char) 18, (char) 19, (char) 20,
        (char) 21, (char) 22, (char) 23, (char) 24, (char) 25, (char) 26, (char) 27, (char) 28, (char) 29, (char) 30,
        (char) 31, ':', '*', '?', '\\', '/'
    };
    private const string FileOrFolderDoesNotExist = "This file/folder doesn't exist.";
    private const string FileInaccessible = "Unable to access the file.";
    private const string DirectoryEmpty = "This directory is empty.";
    private const string InvalidSignatureFile = "Please specify a valid signature file.";
    private const string SignatureFileInaccessible = "Unable to access the signature file.";

    public static string GetFileEncryptionError(string inputFilePath)
    {
        if (Path.GetFileName(Path.TrimEndingDirectorySeparator(inputFilePath)).IndexOfAny(IllegalFileNameChars) != -1) { return "This file/directory name contains illegal characters for Windows, Linux, and/or macOS.";}
        if (Directory.Exists(inputFilePath)) { return FileHandling.IsDirectoryEmpty(inputFilePath) ? DirectoryEmpty : null; }
        if (!File.Exists(inputFilePath)) { return FileOrFolderDoesNotExist; }
        bool? validMagicBytes = FileHandling.IsKryptorFile(inputFilePath);
        if (validMagicBytes == null) { return FileInaccessible; }
        if (FileHandling.HasKryptorExtension(inputFilePath) || validMagicBytes == true) { return "This file has already been encrypted."; }
        return null;
    }

    public static string KeyfilePath(string keyfilePath)
    {
        try
        {
            if (string.IsNullOrEmpty(keyfilePath) || File.Exists(keyfilePath)) { return keyfilePath; }
            if (Directory.Exists(keyfilePath)) { keyfilePath = Path.Combine(keyfilePath, FileHandling.GetRandomFileName()); }
            if (!keyfilePath.EndsWith(Constants.KeyfileExtension)) { keyfilePath += Constants.KeyfileExtension; }
            if (File.Exists(keyfilePath)) { return keyfilePath; }
            Keyfiles.GenerateKeyfile(keyfilePath);
            Console.WriteLine($"Randomly generated keyfile: {Path.GetFileName(keyfilePath)}");
            Console.WriteLine();
            DisplayMessage.WriteLine("IMPORTANT: Please back up this keyfile to external storage (e.g. memory sticks).", ConsoleColor.Blue);
            Console.WriteLine();
            return keyfilePath;
        }
        catch (Exception ex) when (ExceptionFilters.FileAccess(ex))
        {
            DisplayMessage.FilePathException(keyfilePath, ex.GetType().Name, "Unable to randomly generate a keyfile.");
            return null;
        }
    }

    public static string GetFileDecryptionError(string inputFilePath)
    {
        if (Directory.Exists(inputFilePath)) { return FileHandling.IsDirectoryEmpty(inputFilePath) ? DirectoryEmpty : null; }
        if (!File.Exists(inputFilePath)) { return FileOrFolderDoesNotExist; }
        bool? validMagicBytes = FileHandling.IsKryptorFile(inputFilePath);
        if (validMagicBytes == null) { return FileInaccessible; }
        if (!FileHandling.HasKryptorExtension(inputFilePath) || validMagicBytes == false) { return "This file hasn't been encrypted."; }
        return null;
    }

    public static bool GenerateKeyPair(string directoryPath, int keyPairType)
    {
        IEnumerable<string> errorMessages = GetGenerateKeyPairError(directoryPath, keyPairType);
        return DisplayMessage.AnyErrors(errorMessages);
    }

    private static IEnumerable<string> GetGenerateKeyPairError(string directoryPath, int keyPairType)
    {
        if (keyPairType is < 1 or > 2) { yield return "Please enter a valid number.";}
        bool defaultKeyDirectory = string.Equals(directoryPath, Constants.DefaultKeyDirectory);
        if (!defaultKeyDirectory && !Directory.Exists(directoryPath))
        {
            yield return "This directory doesn't exist.";
        }
        else if (defaultKeyDirectory && !Globals.Overwrite)
        {
            if (keyPairType == 1 && (File.Exists(Constants.DefaultEncryptionPublicKeyPath) || File.Exists(Constants.DefaultEncryptionPrivateKeyPath)))
            {
                yield return "An encryption key pair already exists. Please specify -o|--overwrite if you want to overwrite your key pair.";
            }
            else if (keyPairType == 2 && (File.Exists(Constants.DefaultSigningPublicKeyPath) || File.Exists(Constants.DefaultSigningPrivateKeyPath)))
            {   
                yield return "A signing key pair already exists. Please specify -o|--overwrite if you want to overwrite your key pair.";
            }
        }
        else if (!defaultKeyDirectory && !Globals.Overwrite && keyPairType == 1)
        {
            string publicKeyPath = Path.Combine(directoryPath, Constants.DefaultEncryptionKeyFileName + Constants.PublicKeyExtension);
            string privateKeyPath = Path.Combine(directoryPath, Constants.DefaultEncryptionKeyFileName + Constants.PrivateKeyExtension);
            if (File.Exists(publicKeyPath) || File.Exists(privateKeyPath))
            {
                yield return "An encryption key pair already exists in the specified directory. Please specify -o|--overwrite if you want to overwrite your key pair.";
            }
        }
        else if (!defaultKeyDirectory && !Globals.Overwrite && keyPairType == 2)
        {
            string publicKeyPath = Path.Combine(directoryPath, Constants.DefaultSigningKeyFileName + Constants.PublicKeyExtension);
            string privateKeyPath = Path.Combine(directoryPath, Constants.DefaultSigningKeyFileName + Constants.PrivateKeyExtension);
            if (File.Exists(publicKeyPath) || File.Exists(privateKeyPath))
            {
                yield return "A signing key pair already exists in the specified directory. Please specify -o|--overwrite if you want to overwrite your key pair.";
            }
        }
    }

    public static bool RecoverPublicKey(string privateKeyPath)
    {
        IEnumerable<string> errorMessages = GetRecoverPublicKeyError(privateKeyPath);
        return DisplayMessage.AnyErrors(errorMessages);
    }

    private static IEnumerable<string> GetRecoverPublicKeyError(string privateKeyPath)
    {
        if (string.IsNullOrEmpty(privateKeyPath))
        {
            yield return "Please specify a private key using [-x:file].";
        }
        else if (!string.IsNullOrEmpty(privateKeyPath) && !privateKeyPath.EndsWith(Constants.PrivateKeyExtension))
        {
            yield return ErrorMessages.InvalidPrivateKeyFile;
        }
        else if (!string.IsNullOrEmpty(privateKeyPath) && !File.Exists(privateKeyPath))
        {
            yield return ErrorMessages.NonExistentPrivateKeyFile;
        }
    }

    public static IEnumerable<string> GetFileSigningError(string filePath, string[] signatureFilePaths)
    {
        if (Directory.Exists(filePath))
        {
            if (FileHandling.IsDirectoryEmpty(filePath)) { yield return DirectoryEmpty; }
            if (signatureFilePaths != null) { yield return "You cannot specify signature files when signing a directory."; }
        }
        else if (!File.Exists(filePath))
        {
            yield return FileOrFolderDoesNotExist;
        }
    }

    public static string GetSignatureVerifyError(string filePath, string signatureFilePath)
    {
        if (filePath.EndsWith(Constants.SignatureExtension)) { return "Please specify the file to verify, not the signature file."; }
        if (Directory.Exists(filePath)) { return "Please only specify files, not directories."; }
        if (!File.Exists(filePath)) { return "This file doesn't exist."; }
        if (string.IsNullOrEmpty(signatureFilePath)) { return null; }
        if (!File.Exists(signatureFilePath)) { return "Unable to find the signature file. Please specify it manually using -t|--signature."; }
        bool? validMagicBytes = FileHandling.IsSignatureFile(signatureFilePath);
        if (validMagicBytes == null) { return SignatureFileInaccessible; }
        return validMagicBytes == false ? "The signature file that was found is not valid." : null;
    }

    public static string GetSignatureFileError(string signatureFilePath)
    {
        if (!signatureFilePath.EndsWith(Constants.SignatureExtension)) { return InvalidSignatureFile; }
        if (!File.Exists(signatureFilePath)) { return "Please specify a signature file that exists."; }
        bool? validMagicBytes = FileHandling.IsSignatureFile(signatureFilePath);
        if (validMagicBytes == null) { return SignatureFileInaccessible; }
        return validMagicBytes == false ? InvalidSignatureFile : null;
    }
}