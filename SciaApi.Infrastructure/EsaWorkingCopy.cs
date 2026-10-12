using System;
using System.IO;
using System.Security.Cryptography;

namespace SciaApi.Infrastructure
{
    /// <summary>
    /// Creates working copies of SCIA .esa project files so that the original input is never modified
    /// </summary>
    public static class EsaWorkingCopy
    {
        /// <summary>
        /// Copies an existing .esa file to a new .esa file (never overwrites) and clears the read-only attribute of the copy
        /// </summary>
        /// <param name="inputPath">Existing .esa file to copy; it is only read</param>
        /// <param name="outputPath">Path of the new .esa file; its folder is created if it does not exist</param>
        /// <returns>Full path of the created copy</returns>
        /// <exception cref="ArgumentException">A path is empty, a file is not an .esa file, or the output equals the input</exception>
        /// <exception cref="FileNotFoundException">The input file does not exist</exception>
        /// <exception cref="IOException">The output file or folder already exists</exception>
        public static string CreateCopy(string inputPath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                throw new ArgumentException("Input path must not be empty.", nameof(inputPath));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path must not be empty.", nameof(outputPath));
            }

            string fullInputPath = Path.GetFullPath(inputPath);
            string fullOutputPath = Path.GetFullPath(outputPath);

            if (!File.Exists(fullInputPath))
            {
                throw new FileNotFoundException($"Input .esa file not found: {fullInputPath}", fullInputPath);
            }

            if (!HasEsaExtension(fullInputPath))
            {
                throw new ArgumentException($"Input must be an .esa file: {fullInputPath}", nameof(inputPath));
            }

            if (!HasEsaExtension(fullOutputPath))
            {
                throw new ArgumentException($"Output must be an .esa file: {fullOutputPath}", nameof(outputPath));
            }

            if (string.Equals(fullInputPath, fullOutputPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Output must differ from input so that the original file is never modified.", nameof(outputPath));
            }

            if (File.Exists(fullOutputPath) || Directory.Exists(fullOutputPath))
            {
                throw new IOException($"Output already exists and is not overwritten: {fullOutputPath}");
            }

            string outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.Copy(fullInputPath, fullOutputPath, false);

            FileAttributes attributes = File.GetAttributes(fullOutputPath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(fullOutputPath, attributes & ~FileAttributes.ReadOnly);
            }

            return fullOutputPath;
        }

        /// <summary>
        /// Computes the SHA-256 hash of a file
        /// </summary>
        /// <param name="path">File to hash</param>
        /// <returns>Uppercase hexadecimal hash without separators</returns>
        public static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static bool HasEsaExtension(string path)
        {
            return string.Equals(Path.GetExtension(path), ".esa", StringComparison.OrdinalIgnoreCase);
        }
    }
}
