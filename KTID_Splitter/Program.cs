using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Amicitia.IO.Binary;

namespace KTID_Splitter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                System.Console.WriteLine("Usage:\nKTID_Splitter [path to g1t] [path to ktid] [path to kidssingletondb] ");
            }
            else
            {
                FileInfo arg0 = new FileInfo(args[0]);

                if (arg0.Extension == ".g1t")
                {
                    List<Int32> g1tPointers = new List<Int32>();
                    List<Int32> g1tNormalFlag = new List<Int32>();
                    List<UInt32> texHashes = new List<UInt32>();

                    byte[] SectionBytes;

                    var g1tmagic = 0;
                    var g1tversion = 0;
                    var texcount = 0;
                    var offsetTable = 0;
                    var platformID = 0;
                    var unk = 0;

                    // Read hash list from KITD
                    FileInfo arg1 = new FileInfo(args[1]);
                    if (arg1.Extension == ".ktid")
                    {
                        Console.WriteLine("\nReading " + arg1.Name);
                        using (BinaryObjectReader ktidFile = new BinaryObjectReader(args[1], Endianness.Little, Encoding.GetEncoding(65001)))
                        {
                            var numOfTex = arg1.Length / 8;

                            Console.WriteLine("Number of Entries in KTID: " + numOfTex.ToString());

                            for (var i = 0; i < numOfTex; i++)
                            {
                                ktidFile.Seek(4, SeekOrigin.Current);
                                texHashes.Add(ktidFile.ReadUInt32());
                                //Console.WriteLine("Reading Hash: " + i.ToString() + " = " + texHashes[i]);
                            }
                        }
                    }

                    // Parse kidsobj database to get name hashes
                    FileInfo arg2 = new FileInfo(args[2]);
                    Console.WriteLine("\nParsing " + arg2.Name);
                    List<UInt32> ktidEntryID = new List<UInt32>();
                    List<UInt32> textureNameHash = new List<UInt32>();

                    using (BinaryObjectReader kidsobj = new BinaryObjectReader(args[2], Endianness.Little, Encoding.GetEncoding(65001)))
                    {
                        var fileMagic = kidsobj.ReadInt64();
                        var headerSize = kidsobj.ReadInt32();
                        var field_0xC = kidsobj.ReadInt32();
                        var numOfEntries = kidsobj.ReadInt32();
                        var kidsobjfileID = kidsobj.ReadUInt32();
                        var totalFileSize = kidsobj.ReadInt32();

                        Console.WriteLine("Number of Entries in KIDSOBJ: " + numOfEntries.ToString());

                        Console.WriteLine("Parsing texture name hash list");
                        for (var i = 0; i < numOfEntries; i++)
                        {
                            fileMagic = kidsobj.ReadInt64(); //0x0
                            var entrySize = kidsobj.ReadUInt32();
                            kidsobjfileID = kidsobj.ReadUInt32();
                            var typeHash = kidsobj.ReadUInt32();
                            var numOfProperties = kidsobj.ReadUInt32();
                            //Console.WriteLine("entryID 0x" + kidsobjfileID.ToString("X8"));
                            //Console.WriteLine("entry Size: " + entrySize.ToString() + " : Expected Size " + ( 24 +  (numOfProperties * 16 )).ToString());
                            if (typeHash == 4286431188) // 0xFF7DBFD4 TypeInfo::Object::Render::Texture::Static
                            {
                                //Console.WriteLine("Found TypeInfo::Object::Render::Texture::Static");
                                ktidEntryID.Add(kidsobjfileID);
                                for (var h = 0; h < numOfProperties; h++)
                                {
                                    var propertyType = kidsobj.ReadInt32();
                                    var count = kidsobj.ReadInt32();
                                    var kidsTypeHash = kidsobj.ReadInt32();
                                }
                                textureNameHash.Add(kidsobj.ReadUInt32());
                                for (var h = 0; h < numOfProperties - 1; h++)
                                {
                                    var property = kidsobj.ReadInt32();
                                }
                            }
                            else
                            {
                                var dataPadding = (4 - entrySize % 4) % 4;
                                //Console.WriteLine("Property 0x" + typeHash.ToString("X8"));
                                kidsobj.Seek((entrySize + dataPadding) - 24, SeekOrigin.Current);
                            }
                        }
                    }

                    // Read G1T
                    using (BinaryObjectReader g1tFile = new BinaryObjectReader(args[0], Endianness.Little, Encoding.GetEncoding(65001)))
                    {
                        Console.WriteLine("\nProcessing " + arg0.Name);
                        g1tmagic = g1tFile.ReadInt32();
                        g1tversion = g1tFile.ReadInt32();
                        var filesize = g1tFile.ReadInt32();//irrelevant
                        offsetTable = g1tFile.ReadInt32();
                        texcount = g1tFile.ReadInt32();
                        platformID = g1tFile.ReadInt32();
                        unk = g1tFile.ReadInt32();
                        Console.WriteLine("Number of Textures in G1T: " + texcount.ToString());

                        for (var i = 0; i < texcount; i++)
                        {
                            g1tNormalFlag.Add(g1tFile.ReadInt32());
                        }

                        g1tFile.Seek(offsetTable, SeekOrigin.Begin);

                        for (var i = 0; i < texcount; i++)
                        {
                            g1tPointers.Add(g1tFile.ReadInt32() + offsetTable);
                            Console.WriteLine("Pointer # " + (i + 1).ToString() + " -> 0x" + g1tPointers[i].ToString("X8"));
                        }
                        g1tPointers.Add((int)arg0.Length);
                        Console.WriteLine("\n");
                        for (var i = 0; i < texcount; i++)
                        {
                            g1tFile.Seek(g1tPointers[i], SeekOrigin.Begin);
                            var dataSize = (g1tPointers[i + 1] - g1tPointers[i]);
                            //Console.WriteLine("Current Est. Size: 0x" + dataSize.ToString("X8"));

                            SectionBytes = g1tFile.ReadArray<byte>(dataSize);
                            //Console.WriteLine("Actual amount of bytes read: 0x" + SectionBytes.Length.ToString("X8"));
                            String savePath;
                            //try to write individual G1T
                            if (args.Length > 3)
                            {
                                savePath = args[3];
                                System.IO.Directory.CreateDirectory(args[3]);
                            }
                            else savePath = arg0.DirectoryName;

                            string hashFileName = Path.Combine(savePath, "0x" + texHashes[i].ToString("X8") + ".file");
                            
                            for (var h = 0; h < ktidEntryID.Count; h++)
                            {
                                if (texHashes[i] == ktidEntryID[h])
                                {
                                    hashFileName = Path.Combine(savePath, "0x" + textureNameHash[i].ToString("X8") + ".file");
                                    Console.WriteLine("KTID Match Found: " + ktidEntryID[h].ToString("X8") + " => " + textureNameHash[h].ToString("X8"));
                                    h = 69420;
                                }
                            }

                            Console.WriteLine("Writing G1T File # " + (i + 1).ToString() + " -> " + hashFileName);
                            using (BinaryObjectWriter newG1TFile = new BinaryObjectWriter(hashFileName, Endianness.Little, Encoding.GetEncoding(65001)))
                            {
                                newG1TFile.WriteInt32(g1tmagic);
                                newG1TFile.WriteInt32(g1tversion);
                                newG1TFile.WriteInt32(0); // TODO Fix this, its filesize
                                newG1TFile.WriteInt32(32); // offset table, only 1
                                newG1TFile.WriteInt32(1); // only 1 tex per g1t
                                newG1TFile.WriteInt32(platformID);
                                newG1TFile.WriteInt32(0); // padding?
                                newG1TFile.WriteInt32(g1tNormalFlag[i]);
                                newG1TFile.WriteInt32(4); //offset table
                                newG1TFile.WriteBytes(SectionBytes);
                                // Go back and fix filesize
                                newG1TFile.Seek(8, SeekOrigin.Begin);
                                newG1TFile.WriteInt32((int)newG1TFile.Length);
                            }
                        }
                    }
                }
                else if (arg0.Extension == ".ktid")
                {
                    Console.WriteLine("Dumping KTID list to txt");
                    using (BinaryObjectReader ktidFile = new BinaryObjectReader(args[0], Endianness.Little, Encoding.GetEncoding(65001)))
                    {
                        List<string> texHashes = new List<string>();
                        var numOfTex = arg0.Length / 8;

                        Console.WriteLine("Number of textures: " + numOfTex.ToString());

                        for (var i = 0; i < numOfTex; i++)
                        {
                            ktidFile.Seek(4, SeekOrigin.Current);
                            texHashes.Add(ktidFile.ReadUInt32().ToString("X8"));
                            Console.WriteLine("Reading Hash: " + i.ToString() + " = " + texHashes[i]);
                        }

                        var savePath = arg0.FullName + ".txt";
                        File.WriteAllLines(savePath, texHashes);
                        Console.WriteLine("File saved to: " + savePath, "File saved");
                    }
                }
                else if (arg0.Extension == ".txt")
                {
                    Console.WriteLine("Converting KTID List to KTID file");

                    string[] readText = File.ReadAllLines(args[0], Encoding.UTF8);

                    List<string> texHashes = new List<string>();

                    int k = 0;
                    foreach (string s in readText)
                    {
                        texHashes.Add(readText[k]);
                        k++;
                    }

                    var savePath = arg0.FullName + ".ktid";
                    using (BinaryObjectWriter newKTIDFile = new BinaryObjectWriter(savePath, Endianness.Little, Encoding.GetEncoding(65001)))
                    {
                        for (var i = 0; i < texHashes.Count; i++)
                        {
                            newKTIDFile.WriteInt32(i);
                            newKTIDFile.WriteInt32(Convert.ToInt32(texHashes[i], 16));
                        }
                        Console.WriteLine("File saved to: " + savePath, "File saved");
                    }
                }
                else if (arg0.Extension == ".kidssingletondb")
                {
                    Console.WriteLine("Parsing CharacterEditor.kidssingletondb");
                    List<UInt32> ktidEntryID = new List<UInt32>();
                    List<UInt32> textureNameHash = new List<UInt32>();

                    using (BinaryObjectReader kidsobj = new BinaryObjectReader(args[0], Endianness.Little, Encoding.GetEncoding(65001)))
                    {
                        var fileMagic = kidsobj.ReadInt64();
                        var headerSize = kidsobj.ReadInt32();
                        var field_0xC = kidsobj.ReadInt32();
                        var numOfEntries = kidsobj.ReadInt32();
                        var kidsobjfileID = kidsobj.ReadUInt32();
                        var totalFileSize = kidsobj.ReadInt32();

                        Console.WriteLine("Number of Entries in KIDSOBJ: " + numOfEntries.ToString());

                        Console.WriteLine("Parsing and dumping texture name hash list");
                        for (var i = 0; i < numOfEntries; i++)
                        {
                            fileMagic = kidsobj.ReadInt64(); //0x0
                            var entrySize = kidsobj.ReadUInt32();
                            kidsobjfileID = kidsobj.ReadUInt32();
                            var typeHash = kidsobj.ReadUInt32();
                            var numOfProperties = kidsobj.ReadUInt32();
                            //Console.WriteLine("entryID 0x" + kidsobjfileID.ToString("X8"));
                            //Console.WriteLine("entry Size: " + entrySize.ToString() + " : Expected Size " + ( 24 +  (numOfProperties * 16 )).ToString());
                            if (typeHash == 4286431188) // 0xFF7DBFD4 TypeInfo::Object::Render::Texture::Static
                            {
                                //Console.WriteLine("Found TypeInfo::Object::Render::Texture::Static");
                                ktidEntryID.Add(kidsobjfileID);
                                for (var h = 0; h < numOfProperties; h++)
                                {
                                    var propertyType = kidsobj.ReadInt32();
                                    var count = kidsobj.ReadInt32();
                                    var kidsTypeHash = kidsobj.ReadInt32();
                                }
                                textureNameHash.Add(kidsobj.ReadUInt32());
                                for (var h = 0; h < numOfProperties - 1; h++)
                                {
                                    var property = kidsobj.ReadInt32();
                                }
                            }
                            else
                            {
                                var dataPadding = (4 - entrySize % 4) % 4;
                                //Console.WriteLine("Property 0x" + typeHash.ToString("X8"));
                                kidsobj.Seek((entrySize + dataPadding) - 24, SeekOrigin.Current);
                            }
                        }

                        List<String> combination = new List<String>();

                        var savePath = arg0.FullName + ".txt";
                        for (var h = 0; h < textureNameHash.Count; h++)
                        {
                            combination.Add(ktidEntryID[h].ToString("X8") + " " + textureNameHash[h].ToString("X8"));
                        }
                        File.WriteAllLines(savePath, combination);
                        Console.WriteLine("File saved to: " + savePath, "File saved");
                    }
                }
                else Console.WriteLine("Error: First file is not a ktid or g1t file.");
            }
        }
    }
}
