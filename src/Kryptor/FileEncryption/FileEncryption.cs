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
using System.Security.Cryptography;
using Geralt;

namespace Kryptor;

public static class FileEncryption
{
    public static void EncryptEachFileWithPassword(string[] filePaths, Span<byte> password)
    {
        if (filePaths == null || password == default) {
            throw new UserInputException();
        }
        Span<byte> salt = stackalloc byte[Argon2id.SaltSize];
        Span<byte> ephemeralPublicKey = stackalloc byte[X25519.PublicKeySize], ephemeralPrivateKey = stackalloc byte[X25519.PrivateKeySize];
        Span<byte> headerKey = stackalloc byte[Constants.HeaderKeySize];
        foreach (string inputFilePath in filePaths) {
            try
            {
                bool isDirectory = IsDirectory(inputFilePath, out string zipFilePath);
                SecureRandom.Fill(salt);
                // Fill unused header with random public key
                X25519.GenerateKeyPair(ephemeralPublicKey, ephemeralPrivateKey);
                DisplayMessage.DerivingKeyFromPassword();
                Argon2id.DeriveKey(headerKey, password, salt, Constants.Iterations, Constants.MemorySize);
                EncryptInputFile(isDirectory ? zipFilePath : inputFilePath, isDirectory, ephemeralPublicKey, salt, headerKey);
            }
            catch (Exception ex) when (ExceptionFilters.Cryptography(ex))
            {
                DisplayMessage.FilePathException(inputFilePath, ex.GetType().Name, ErrorMessages.UnableToEncryptFile);
            }
            Console.WriteLine();
        }
        CryptographicOperations.ZeroMemory(password);
        DisplayMessage.SuccessfullyEncrypted(insertSpace: false);
    }
    
    public static void EncryptEachFileWithSymmetricKey(string[] filePaths, Span<byte> symmetricKey)
    {
        if (filePaths == null || symmetricKey == default) {
            throw new UserInputException();
        }
        Span<byte> salt = stackalloc byte[BLAKE2b.SaltSize];
        Span<byte> ephemeralPublicKey = stackalloc byte[X25519.PublicKeySize], ephemeralPrivateKey = stackalloc byte[X25519.PrivateKeySize];
        Span<byte> headerKey = stackalloc byte[Constants.HeaderKeySize];
        foreach (string inputFilePath in filePaths) {
            try
            {
                bool isDirectory = IsDirectory(inputFilePath, out string zipFilePath);
                SecureRandom.Fill(salt);
                // Fill unused header with random public key
                X25519.GenerateKeyPair(ephemeralPublicKey, ephemeralPrivateKey);
                BLAKE2b.DeriveKey(headerKey, symmetricKey, Constants.Personalisation, salt);
                EncryptInputFile(isDirectory ? zipFilePath : inputFilePath, isDirectory, ephemeralPublicKey, salt, headerKey);
            }
            catch (Exception ex) when (ExceptionFilters.Cryptography(ex))
            {
                DisplayMessage.FilePathException(inputFilePath, ex.GetType().Name, ErrorMessages.UnableToEncryptFile);
            }
            Console.WriteLine();
        }
        CryptographicOperations.ZeroMemory(symmetricKey);
        DisplayMessage.SuccessfullyEncrypted(insertSpace: false);
    }
    
    public static void EncryptEachFileWithPublicKey(Span<byte> senderPrivateKey, List<byte[]> recipientPublicKeys, Span<byte> preSharedKey, string[] filePaths)
    {
        if (filePaths == null || senderPrivateKey == default || recipientPublicKeys == null) {
            throw new UserInputException();
        }
        Globals.TotalCount *= recipientPublicKeys.Count;
        bool overwrite = Globals.Overwrite;
        Globals.Overwrite = false;
        int i = 0;
        Span<byte> sharedSecret = stackalloc byte[X25519.SharedSecretSize], ephemeralSharedSecret = stackalloc byte[X25519.SharedSecretSize];;
        Span<byte> salt = stackalloc byte[BLAKE2b.SaltSize];
        Span<byte> ephemeralPublicKey = stackalloc byte[X25519.PublicKeySize], ephemeralPrivateKey = stackalloc byte[X25519.PrivateKeySize];
        Span<byte> inputKeyingMaterial = stackalloc byte[ephemeralSharedSecret.Length + sharedSecret.Length];
        Span<byte> headerKey = stackalloc byte[Constants.HeaderKeySize];
        foreach (Span<byte> recipientPublicKey in recipientPublicKeys) {
            if (i++ == recipientPublicKeys.Count - 1) {
                Globals.Overwrite = overwrite;
            }
            X25519.DeriveSenderSharedSecret(sharedSecret, senderPrivateKey, recipientPublicKey, preSharedKey);
            foreach (string inputFilePath in filePaths) {
                Console.WriteLine();
                try
                {
                    bool isDirectory = IsDirectory(inputFilePath, out string zipFilePath);
                    X25519.GenerateKeyPair(ephemeralPublicKey, ephemeralPrivateKey);
                    X25519.DeriveSenderSharedSecret(ephemeralSharedSecret, ephemeralPrivateKey, recipientPublicKey, preSharedKey);
                    CryptographicOperations.ZeroMemory(ephemeralPrivateKey);
                    SecureRandom.Fill(salt);
                    Spans.Concat(inputKeyingMaterial, ephemeralSharedSecret, sharedSecret);
                    BLAKE2b.DeriveKey(headerKey, inputKeyingMaterial, Constants.Personalisation, salt);
                    CryptographicOperations.ZeroMemory(ephemeralSharedSecret);
                    CryptographicOperations.ZeroMemory(inputKeyingMaterial);
                    EncryptInputFile(isDirectory ? zipFilePath : inputFilePath, isDirectory, ephemeralPublicKey, salt, headerKey);
                }
                catch (Exception ex) when (ExceptionFilters.Cryptography(ex))
                {
                    DisplayMessage.FilePathException(inputFilePath, ex.GetType().Name, ErrorMessages.UnableToEncryptFile);
                }
            }
            CryptographicOperations.ZeroMemory(sharedSecret);
        }
        CryptographicOperations.ZeroMemory(senderPrivateKey);
        CryptographicOperations.ZeroMemory(preSharedKey);
        DisplayMessage.SuccessfullyEncrypted();
    }

    public static void EncryptEachFileWithPrivateKey(Span<byte> privateKey, Span<byte> preSharedKey, string[] filePaths)
    {
        if (filePaths == null || privateKey == default) {
            throw new UserInputException();
        }
        Span<byte> salt = stackalloc byte[BLAKE2b.SaltSize];
        Span<byte> ephemeralPublicKey = stackalloc byte[X25519.PublicKeySize], ephemeralPrivateKey = stackalloc byte[X25519.PrivateKeySize];
        Span<byte> ephemeralSharedSecret = stackalloc byte[X25519.SharedSecretSize];
        Span<byte> headerKey = stackalloc byte[Constants.HeaderKeySize];
        foreach (string inputFilePath in filePaths) {
            Console.WriteLine();
            try
            {
                bool isDirectory = IsDirectory(inputFilePath, out string zipFilePath);
                X25519.GenerateKeyPair(ephemeralPublicKey, ephemeralPrivateKey);
                CryptographicOperations.ZeroMemory(ephemeralPrivateKey);
                X25519.DeriveSenderSharedSecret(ephemeralSharedSecret, privateKey, ephemeralPublicKey, preSharedKey);
                SecureRandom.Fill(salt);
                BLAKE2b.DeriveKey(headerKey, ephemeralSharedSecret, Constants.Personalisation, salt);
                CryptographicOperations.ZeroMemory(ephemeralSharedSecret);
                EncryptInputFile(isDirectory ? zipFilePath : inputFilePath, isDirectory, ephemeralPublicKey, salt, headerKey);
            }
            catch (Exception ex) when (ExceptionFilters.Cryptography(ex))
            {
                DisplayMessage.FilePathException(inputFilePath, ex.GetType().Name, ErrorMessages.UnableToEncryptFile);
            }
        }
        CryptographicOperations.ZeroMemory(privateKey);
        CryptographicOperations.ZeroMemory(preSharedKey);
        DisplayMessage.SuccessfullyEncrypted();
    }

    private static bool IsDirectory(string inputFilePath, out string zipFilePath)
    {
        bool isDirectory = File.GetAttributes(inputFilePath).HasFlag(FileAttributes.Directory);
        zipFilePath = inputFilePath + Constants.ZipFileExtension;
        if (isDirectory) {
            FileHandling.CreateZipFile(inputFilePath, zipFilePath);
        }
        return isDirectory;
    }
    
    private static void EncryptInputFile(string inputFilePath, bool isDirectory, Span<byte> ephemeralPublicKey, Span<byte> salt, Span<byte> headerKey)
    {
        string outputFilePath = !Globals.EncryptFileNames ? inputFilePath : FileHandling.ReplaceFileName(inputFilePath, SecureRandom.GetString(Constants.RandomFileNameLength));
        outputFilePath = FileHandling.GetUniqueFilePath(outputFilePath + Constants.EncryptedExtension);
        DisplayMessage.InputToOutput("Encrypting", inputFilePath, outputFilePath);
        
        Span<byte> unencryptedHeaders = stackalloc byte[Constants.UnencryptedHeadersLength];
        Spans.Concat(unencryptedHeaders, Constants.EncryptionMagicBytes, Constants.EncryptionVersion, ephemeralPublicKey, salt);
        
        Span<byte> encryptionKey = headerKey[..ChaCha20.KeySize];
        Span<byte> nonce = headerKey[encryptionKey.Length..];
        
        EncryptFile.Encrypt(inputFilePath, outputFilePath, isDirectory, unencryptedHeaders, nonce, encryptionKey);
        Globals.SuccessfulCount++;
    }
}