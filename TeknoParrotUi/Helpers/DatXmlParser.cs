using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace TeknoParrotUi.Views
{
    public class DatXmlParser
    {
        public class DatFile
        {
            public DatHeader Header { get; set; }
            public List<DatGame> Games { get; set; } = new List<DatGame>();
        }

        public class DatHeader
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Category { get; set; }
            public string Version { get; set; }
            public string Date { get; set; }
            public string Author { get; set; }
            public string Comment { get; set; }
            public string Homepage { get; set; }
        }

        public class DatGame
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string GameProfile { get; set; }
            public string Executable { get; set; }
            public string Executable2 { get; set; }
            public string TestMenuExecutable { get; set; }
            public List<DatRom> Roms { get; set; } = new List<DatRom>();
        }

        public class DatRom
        {
            public string Name { get; set; }
            public long Size { get; set; }
            public string Crc { get; set; }
            public string Md5 { get; set; }
            public string Sha1 { get; set; }
        }

        /// <summary>
        /// Parses a DAT file using streaming to minimize memory usage
        /// </summary>
        public static DatFile ParseDatFile(string filePath)
        {
            var datFile = new DatFile
            {
                Header = new DatHeader()
            };

            using (XmlReader reader = XmlReader.Create(filePath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
            {
                DatGame currentGame = null;
                bool inHeader = false;

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        switch (reader.Name)
                        {
                            case "header":
                                inHeader = true;
                                break;

                            case "name":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    datFile.Header.Name = reader.Value;
                                else if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.Name = reader.Value;
                                break;

                            case "description":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    datFile.Header.Description = reader.Value;
                                else if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.Description = reader.Value;
                                break;

                            case "category":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    datFile.Header.Category = reader.Value;
                                break;

                            case "version":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    datFile.Header.Version = reader.Value;
                                break;

                            case "date":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    datFile.Header.Date = reader.Value;
                                break;

                            case "author":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    datFile.Header.Author = reader.Value;
                                break;

                            case "comment":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    datFile.Header.Comment = reader.Value;
                                break;

                            case "homepage":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    datFile.Header.Homepage = reader.Value;
                                break;

                            case "game":
                                inHeader = false;
                                // Start a new game
                                currentGame = new DatGame();
                                
                                // Get name attribute if present
                                if (reader.HasAttributes)
                                {
                                    string name = reader["name"];
                                    if (!string.IsNullOrEmpty(name))
                                        currentGame.Name = name;
                                }
                                break;

                            case "GameProfile":
                                if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.GameProfile = reader.Value;
                                break;

                            case "Executable":
                                if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.Executable = reader.Value;
                                break;

                            case "Executable2":
                                if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.Executable2 = reader.Value;
                                break;

                            case "TestMenuExecutable":
                                if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.TestMenuExecutable = reader.Value;
                                break;

                            case "rom":
                                if (currentGame != null && reader.HasAttributes)
                                {
                                    var rom = new DatRom();

                                    rom.Name = reader["name"];
                                    
                                    string sizeStr = reader["size"];
                                    if (!string.IsNullOrEmpty(sizeStr) && long.TryParse(sizeStr, out long size))
                                        rom.Size = size;

                                    rom.Crc = reader["crc"];
                                    rom.Md5 = reader["md5"];
                                    rom.Sha1 = reader["sha1"];

                                    currentGame.Roms.Add(rom);
                                }
                                break;
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (reader.Name == "header")
                        {
                            inHeader = false;
                        }
                        else if (reader.Name == "game")
                        {
                            // Only add games with a valid GameProfile
                            if (currentGame != null && !string.IsNullOrEmpty(currentGame.GameProfile))
                            {
                                datFile.Games.Add(currentGame);
                            }
                            currentGame = null;
                        }
                    }
                }
            }

            return datFile;
        }

        /// <summary>
        /// Processes a DAT file with a callback to handle each game as it's read
        /// </summary>
        public static void ProcessDatFileStreaming(string filePath, Action<DatHeader> headerCallback, Action<DatGame> gameCallback)
        {
            var header = new DatHeader();
            bool headerProcessed = false;

            using (XmlReader reader = XmlReader.Create(filePath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }))
            {
                DatGame currentGame = null;
                bool inHeader = false;

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        switch (reader.Name)
                        {
                            case "header":
                                inHeader = true;
                                break;

                            case "name":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    header.Name = reader.Value;
                                else if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.Name = reader.Value;
                                break;

                            case "description":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    header.Description = reader.Value;
                                else if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.Description = reader.Value;
                                break;

                            case "category":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    header.Category = reader.Value;
                                break;

                            case "version":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    header.Version = reader.Value;
                                break;

                            case "date":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    header.Date = reader.Value;
                                break;

                            case "author":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    header.Author = reader.Value;
                                break;

                            case "comment":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    header.Comment = reader.Value;
                                break;

                            case "homepage":
                                if (inHeader && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    header.Homepage = reader.Value;
                                break;

                            case "game":
                                inHeader = false;
                                // Start a new game
                                currentGame = new DatGame();
                                
                                // Get name attribute if present
                                if (reader.HasAttributes)
                                {
                                    string name = reader["name"];
                                    if (!string.IsNullOrEmpty(name))
                                        currentGame.Name = name;
                                }
                                break;

                            case "GameProfile":
                                if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.GameProfile = reader.Value;
                                break;

                            case "Executable":
                                if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.Executable = reader.Value;
                                break;

                            case "Executable2":
                                if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.Executable2 = reader.Value;
                                break;

                            case "TestMenuExecutable":
                                if (currentGame != null && reader.Read() && reader.NodeType == XmlNodeType.Text)
                                    currentGame.TestMenuExecutable = reader.Value;
                                break;

                            case "rom":
                                if (currentGame != null && reader.HasAttributes)
                                {
                                    var rom = new DatRom();

                                    rom.Name = reader["name"];
                                    
                                    string sizeStr = reader["size"];
                                    if (!string.IsNullOrEmpty(sizeStr) && long.TryParse(sizeStr, out long size))
                                        rom.Size = size;

                                    rom.Crc = reader["crc"];
                                    rom.Md5 = reader["md5"];
                                    rom.Sha1 = reader["sha1"];

                                    currentGame.Roms.Add(rom);
                                }
                                break;
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (reader.Name == "header")
                        {
                            inHeader = false;
                            if (!headerProcessed)
                            {
                                headerCallback?.Invoke(header);
                                headerProcessed = true;
                            }
                        }
                        else if (reader.Name == "game")
                        {
                            // Process game if it has a valid GameProfile
                            if (currentGame != null && !string.IsNullOrEmpty(currentGame.GameProfile))
                            {
                                gameCallback?.Invoke(currentGame);
                            }
                            currentGame = null;
                        }
                    }
                }
            }
        }
    }
}