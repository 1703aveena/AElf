﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Cryptography.Certificate;
using AElf.Cryptography.ECDSA;
using AElf.Cryptography.ECDSA.Exceptions;
using AElf.Cryptography.SSL;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace AElf.Cryptography
{
    public class AElfKeyStore //: IKeyStore
    {
        private static readonly SecureRandom  _random = new SecureRandom();

        private const string KeyFileExtension = ".ak";
        private const string KeyFolderName = "keys";
        
        private const string PemFileExtension = ".pem";
        private const string PemFolderName = "pems";

        private const string _algo = "AES-256-CFB";
        private readonly string _dataDirectory;
        
        // IsOpen not used.
        public bool IsOpen { get; private set; }

        private readonly List<OpenAccount> _openAccounts;
        
        private readonly TimeSpan _defaultTimeoutToClose = TimeSpan.FromMinutes(10);

        public enum Errors
        {
            None = 0,
            AccountAlreadyUnlocked = 1,
            WrongPassword = 2
        }
        
        public AElfKeyStore(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
            _openAccounts = new List<OpenAccount>();
        }

        private void OpenAsync(string address, string password, TimeSpan? timeoutToClose)
        {
            ECKeyPair kp = ReadKeyPairAsync(address, password);
            
            OpenAccount acc = new OpenAccount();
            acc.KeyPair = kp;

            if (timeoutToClose.HasValue)
            {
                Timer t = new Timer(CloseAccount, acc, timeoutToClose.Value, timeoutToClose.Value);
                acc.CloseTimer = t;
            }
            
            _openAccounts.Add(acc);
        }

        public Errors OpenAsync(string address, string password, bool withTimeout = true)
        {
            if (_openAccounts.Any(x => x.Address.Replace("0x", "") == address.Replace("0x", "")))
                return Errors.AccountAlreadyUnlocked;

            try
            {
                if (withTimeout)
                {
                    OpenAsync(address, password, _defaultTimeoutToClose);
                }
                else
                {
                    OpenAsync(address, password, null);
                }
            }
            catch (InvalidPasswordException e)
            {
                return Errors.WrongPassword;
            }

            return Errors.None;
        }

        private void CloseAccount(object accObj)
        {
            if (!(accObj is OpenAccount openAccount)) return;
            openAccount.Close();
            _openAccounts.Remove(openAccount);
        }
        
        public ECKeyPair GetAccountKeyPair(string address)
        {
            return _openAccounts.FirstOrDefault(oa => oa.Address.Replace("0x", "").Equals(address.Replace("0x", "")))?.KeyPair;
        }

        public ECKeyPair Create(string password)
        {
            ECKeyPair keyPair = new KeyPairGenerator().Generate();
            bool res = WriteKeyPair(keyPair, password);
            return !res ? null : keyPair;
        }

        public bool CreateCertificate(ECKeyPair keyPair, string password, params string[] addresses)
        {
            return WriteCertificate(keyPair, password, addresses);
        }

        public List<string> ListAccounts()
        {
            var dir = GetOrCreateKeystoreDir(KeyFolderName);
            FileInfo[] files = dir.GetFiles("*" + KeyFileExtension);

            return files.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToList();
        }

        public ECKeyPair ReadKeyPairAsync(string address, string password)
        {
            try
            {
                string keyFilePath = GetFileFullPath(address, KeyFileExtension);

                AsymmetricCipherKeyPair p;
                using (var textReader = File.OpenText(keyFilePath))
                {
                    PemReader pr = new PemReader(textReader, new Password(password.ToCharArray()));
                    p = pr.ReadObject() as AsymmetricCipherKeyPair;
                }

                if (p == null)
                    return null;
                
                ECKeyPair kp = new ECKeyPair((ECPrivateKeyParameters) p.Private, (ECPublicKeyParameters) p.Public);

                return kp;
            }
            catch (FileNotFoundException ex)
            {
                throw new KeyStoreNotFoundException("Keystore file not found.", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                throw new KeyStoreNotFoundException("Invalid keystore path.", ex);
            }
            catch (PemException pemEx)
            {
                throw new InvalidPasswordException("Invalid password", pemEx);
            }
            catch (Exception e)
            {
            }

            return null;
        }

        private bool WriteKeyPair(ECKeyPair keyPair, string password)
        {
            if (keyPair?.PrivateKey == null || keyPair.PublicKey == null)
                throw new InvalidKeyPairException("Invalid keypair (null reference).", null);

            if (string.IsNullOrEmpty(password))
            {
                // Why here we can just invoke Console.WriteLine? should we use Logger?
                Console.WriteLine("Invalid password.");
                return false;
            }
                
            
            // Ensure path exists
            GetOrCreateKeystoreDir(KeyFolderName);
            
            string fullPath = null;
            try
            {
                var address = keyPair.GetAddressHex();
                fullPath = GetFileFullPath(address, KeyFileExtension);
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not calculate the address from the keypair.", e);
                return false;
            }
            
            var akp = new AsymmetricCipherKeyPair(keyPair.PublicKey, keyPair.PrivateKey);
            
            using (var writer = File.CreateText(fullPath))
            {
                var pw = new PemWriter(writer);
                pw.WriteObject(akp, _algo, password.ToCharArray(), _random);
                pw.Writer.Close();
            }

            return true;
        }

        private bool WriteCertificate(ECKeyPair keyPair, string password, params string[] addresses)
        {
            if (keyPair?.PrivateKey == null || keyPair.PublicKey == null)
                throw new InvalidKeyPairException("Invalid keypair (null reference).", null);

            if (string.IsNullOrEmpty(password))
            {
                // Why here we can just invoke Console.WriteLine? should we use Logger?
                Console.WriteLine("Invalid password.");
                return false;
            }
            
            // Ensure path exists
            GetOrCreateKeystoreDir(PemFolderName);
            
            string pemFileFullPath = null;
            try
            {
                var address = keyPair.GetAddressHex();
                pemFileFullPath = GetFileFullPath(address, PemFileExtension);
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not calculate the address from the keypair.", e);
                return false;
            }
            
            using (var writer = File.CreateText(pemFileFullPath))
            {
                var certGenerator = new CertGenerator().SetPublicKey(keyPair.PublicKey);
                certGenerator.AddALternativeName(addresses);
                var cert = certGenerator.Generate(keyPair.PrivateKey);
                var pw = new PemWriter(writer);
                pw.WriteObject(cert);
                pw.Writer.Close();
            }

            return true;
        }
        
        /// <summary>
        /// Return the full path of the files 
        /// </summary>
        private string GetFileFullPath(string address, string extension)
        {
            if(!extension.Equals(KeyFileExtension) && !extension.Equals(PemFileExtension))
                throw new Exception("Wrong extension type");
            var path = GetKeyFileFullPathStrict(address.Replace("0x", ""), extension);
            if (File.Exists(path))
            {
                return path;
            }

            return GetKeyFileFullPathStrict("0x" + address.Replace("0x", ""), extension);
        }

        private string GetKeyFileFullPathStrict(string address, string extension)
        {
            if(!extension.Equals(KeyFileExtension) && !extension.Equals(PemFileExtension))
                throw new Exception("Wrong extension type");
            string dirPath =
                GetKeystoreDirectoryPath(extension.Equals(KeyFileExtension) ? KeyFolderName : PemFolderName);
            string filePath = Path.Combine(dirPath, address);
            string filePathWithExtension = Path.ChangeExtension(filePath, extension);

            return filePathWithExtension;
        }

        private DirectoryInfo GetOrCreateKeystoreDir(string folderName)
        {
            try
            {
                string dirPath = GetKeystoreDirectoryPath(folderName);
                return Directory.CreateDirectory(dirPath);
            }
            catch (Exception e)
            {
                throw new KeyStoreNotFoundException("Invalid data directory path", e);
            }
        }

        private string GetKeystoreDirectoryPath(string folderName)
        {
            return Path.Combine(_dataDirectory, folderName);
        }
    }
}