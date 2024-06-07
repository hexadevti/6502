using System.Collections;

namespace Runtime;

public class DiskDrive
{
    private Memory memory { get; set; }

    private string diskPath { get; set; }

    public byte[] diskImage { get; set; }

    public byte[][] diskRawData = new byte[35][];

    public int offset { get; set; }
    public int catalog_track { get; set; }
    public int catalog_sector { get; set; }
    public byte[] disk_info { get; set; }

    public int offset_to_disk_info { get; set; }

    public byte[] translateTable = new byte[] {
                            0x96,0x97,0x9A,0x9B,0x9D,0x9E,0x9F,0xA6,
                            0xA7,0xAB,0xAC,0xAD,0xAE,0xAF,0xB2,0xB3,
                            0xB4,0xB5,0xB6,0xB7,0xB9,0xBA,0xBB,0xBC,
                            0xBD,0xBE,0xBF,0xCB,0xCD,0xCE,0xCF,0xD3,
                            0xD6,0xD7,0xD9,0xDA,0xDB,0xDC,0xDD,0xDE,
                            0xDF,0xE5,0xE6,0xE7,0xE9,0xEA,0xEB,0xEC,
                            0xED,0xEE,0xEF,0xF2,0xF3,0xF4,0xF5,0xF6,
                            0xF7,0xF9,0xFA,0xFB,0xFC,0xFD,0xFE,0xFF
                        };

    /* DO logical order  0 1 2 3 4 5 6 7 8 9 A B C D E F */
    /*    physical order 0 D B 9 7 5 3 1 E C A 8 6 4 2 F */

    /* PO logical order  0 E D C B A 9 8 7 6 5 4 3 2 1 F */
    /*    physical order 0 2 4 6 8 A C E 1 3 5 7 9 B D F */

    public byte[] translateDos33Track = new byte[] {
        0x00, 0x07, 0x0e, 0x06, 0x0d, 0x05, 0x0c, 0x04, 0x0b, 0x03, 0x0a, 0x02, 0x09, 0x01, 0x08, 0x0f };

    public DiskDrive(string dskPath, Memory memory)
    {
        diskPath = dskPath;
        this.memory = memory;

        if (!string.IsNullOrEmpty(diskPath))
            this.diskImage = File.ReadAllBytes(dskPath);
        else
            this.diskImage = new byte[143360];
        offset_to_disk_info = GetOffset(17, 0);
        offset = offset_to_disk_info;


        catalog_track = diskImage[offset_to_disk_info + 1];
        catalog_sector = diskImage[offset_to_disk_info + 2];


    }

    public void SaveImage()
    {
        File.WriteAllBytes(diskPath, diskImage);
    }

    public void MarkSectorUsed(int track, int sector)
    {
        offset = offset_to_disk_info;

        var byte_pair_offset = offset + 0x38 + (track * 4);
        var current = (diskImage[byte_pair_offset] << 8) |
              diskImage[byte_pair_offset + 1];

        current &= 0xffff ^ (1 << (sector));

        diskImage[byte_pair_offset + 0] = (byte)(current >> 8);
        diskImage[byte_pair_offset + 1] = (byte)(current & 0xff);
    }

    public byte GetVolume()
    {
        return diskImage[offset + 0x06];
    }

    public string DiskInfo()
    {
        string info = "";
        var totalTracks = diskImage[offset + 0x34];
        info += "================ Disk Info ===================";
        info += "       Catalog Track: " + diskImage[offset + 0x01] + "\r\n";
        info += "      Catalog Sector: " + diskImage[offset + 0x02] + "\r\n";
        info += "      Release Number: " + diskImage[offset + 0x03] + "\r\n";
        info += "       Volume Number: " + diskImage[offset + 0x06] + "\r\n";
        info += "   Max Track/Sectors: " + diskImage[offset + 0x27] + "\r\n";
        info += "     Last Used Track: " + diskImage[offset + 0x30] + "\r\n";
        info += "Track Alloc Dirction: " + diskImage[offset + 0x31] + "\r\n";
        info += "     Tracks Per Disk: " + diskImage[offset + 0x34] + "\r\n";
        info += "    Sectors Per Disk: " + diskImage[offset + 0x35] + "\r\n";
        info += "    Bytes Per Sector: " + GetInt16(diskImage, offset + 0x36) + "\r\n";

        for (int i = 0x38; i <= 0xff; i += 4)
        {
            if ((i - 0x38) / 4 >= totalTracks)
            { break; }
            info += string.Format("         Track {0}: [{1:X2} {2:X2} {3:X2} {4:X2}]\r\n",
                (i - 0x36) / 4,
                diskImage[offset + i + 0],
                diskImage[offset + i + 1],
                diskImage[offset + i + 2],
                diskImage[offset + i + 3]);
        }


        return info;

    }

    public string PrintCatalog()
    {
        string info = "";
        var track = this.catalog_track;
        var sector = this.catalog_sector;

        info += "================ Catalog ===================\r\n";

        while (track != 0)
        {
            info += String.Format("Track: {0}  Sector: {1}\r\n", track, sector);

            offset = GetOffset(track, sector);

            // For each possible catalog entry.
            for (int i = 0x0b; i < 256; i += 0x23)
            {
                info += String.Format("{0:X2}: ", i);

                // Print file name.
                for (int n = 0x03; n <= 0x20; n++)
                {
                    var ch = (int)(diskImage[offset + i + n]);
                    ch &= 0x7f;
                    if (ch >= 32 && ch < 127)
                    {
                        info += System.Convert.ToChar(ch);
                    }
                    else
                    {
                        info += " ";
                    }
                }

                // File type and flags.
                var file_type = diskImage[offset + i + 2];
                string type_string;
                var locked = false;

                type_string = " ";

                if ((file_type & 0x80) != 0)
                {
                    file_type &= 0x7f;
                    locked = true;
                }

                if (file_type == 0x00)
                {
                    type_string += "TXT ";
                }
                else
                {
                    if ((file_type & 0x01) != 0) { type_string += "IBAS "; }
                    if ((file_type & 0x02) != 0) { type_string += "ABAS "; }
                    if ((file_type & 0x04) != 0) { type_string += "BIN "; }
                    if ((file_type & 0x08) != 0) { type_string += "SEQ "; }
                    if ((file_type & 0x10) != 0) { type_string += "REL "; }
                    if ((file_type & 0x20) != 0) { type_string += "A "; }
                    if ((file_type & 0x40) != 0) { type_string += "B "; }
                }

                if (locked) { type_string += "LKD "; }

                // Track and sector where the file descriptor is.
                info += String.Format("{0:X2}/{1:X2}", diskImage[offset + i + 0],
                                  diskImage[offset + i + 1]);
                info += type_string;

                if (diskImage[offset + i + 0] == 0xff) { info += " DEL"; }

                // Count of sectors used for this file.
                var sectors = GetInt16(diskImage, offset + i + 0x21);

                info += String.Format("sectors: {0}", sectors);

                info += "\r\n";
            }

            // Next sector in the linked list of Catalog entries.
            track = diskImage[offset + 1];
            sector = diskImage[offset + 2];

        }

        return info;
    }

    public string DumpSector(int track, int sector)
    {
        string info = "";
        var text = new byte[16];

        var offset = GetOffset(track, sector);

        info += String.Format("===== Track: {0}   Sector: {1}   Offset: {2} =====\n", track, sector, offset);

        for (int i = 0; i < 256; i++)
        {
            if ((i % 16) == 0)
            {
                info += String.Format("{0:X2}: ", i);
            }

            info += String.Format(" {0:X2}", diskImage[offset + i]);

            var ch = (int)diskImage[offset + i];
            ch &= 0x7f;

            if (ch >= 32 && ch < 127)
            {
                text[i % 16] = (byte)ch;
            }
            else
            {
                text[i % 16] = (byte)'.';
            }

            if ((i % 16) == 15)
            {
                info += String.Format("  ");
                for (int n = 0; n < 16; n++)
                {
                    info += System.Convert.ToChar(text[n]);
                }
                info += "\r\n";
            }
        }

        info += "\r\n";

        return info;
    }

    public byte[] GetSectorData(int track, int sector)
    {
        byte[] output = new byte[256];
        if (track < 35 && sector < 16)
        {
            var offset = GetOffset(track, sector);

            for (int i = 0; i < 256; i++)
            {
                output[i] = diskImage[offset + i];
            }
        }

        return output;
    }

    public void SetSectorData(int track, int sector, byte[] data)
    {
        byte[] output = new byte[256];
        if (track < 35 && sector < 16)
        {
            var offset = GetOffset(track, sector);

            for (int i = 0; i < 256; i++)
            {
                diskImage[offset + i] = data[i];
            }
        }
    }

    public byte[] EncodeByte(byte data)
    {
        byte[] output = new byte[2];
        bool[] bitsEncoded = new bool[16];
        BitArray bitsData = new BitArray(new byte[] { data });

        for (int i = 0; i < 16; i++)
        {
            if (i % 2 == 0)
                bitsEncoded[i] = true;
            else
            {
                if (i > 8)
                    bitsEncoded[i] = bitsData[8 - (i - 7)];
                else
                    bitsEncoded[i] = bitsData[8 - i];
            }
        }

        for (int i = 0; i < 8; i++)
        {
            output[0] = (byte)(output[0] + (bitsEncoded[i] ? Math.Pow(2, 7 - i) : 0));
            output[1] = (byte)(output[1] + (bitsEncoded[i + 8] ? Math.Pow(2, 7 - i) : 0));
        }
        return output;
    }

    public byte[] Checksum(byte volume, byte sector, byte track)
    {
        byte[] output = new byte[2];
        bool[] checkedBits = new bool[16];
        bool[] checkedBitsInverted = new bool[16];
        BitArray bitsVolume = new BitArray(EncodeByte(volume));
        BitArray bitsSector = new BitArray(EncodeByte(sector));
        BitArray bitsTrack = new BitArray(EncodeByte(track));

        for (int i = 0; i < 16; i++)
        {
            int sumBits = (bitsVolume[i] ? 1 : 0) + (bitsSector[i] ? 1 : 0) + (bitsTrack[i] ? 1 : 0);
            switch (sumBits)
            {
                case 0:
                    checkedBits[i] = false;
                    break;
                case 1:
                    checkedBits[i] = true;
                    break;
                case 2:
                    checkedBits[i] = false;
                    break;
                case 3:
                    checkedBits[i] = true;
                    break;
            }
        }
        for (int i = 0; i < 16; i++)
        {
            if (i < 8)
                checkedBitsInverted[i] = checkedBits[7 - i];
            else
                checkedBitsInverted[i] = checkedBits[23 - i];
        }
        for (int i = 0; i < 8; i++)
        {
            output[0] = (byte)(output[0] + (checkedBitsInverted[i] ? Math.Pow(2, 7 - i) : 0));
            output[1] = (byte)(output[1] + (checkedBitsInverted[i + 8] ? Math.Pow(2, 7 - i) : 0));
        }
        return output;
    }

    public byte[] Encode6_2(int track, int sector)
    {
        byte[] input = GetSectorData(track, sector);
        byte[] outputData = new byte[256];

        byte[] outputlast2 = new byte[0x56];

        byte[] outputlast2Encoded = new byte[0x56];
        byte[] outputDataEncoded = new byte[256];

        for (int i = 0; i < input.Length; i++)
        {
            outputData[i] = (byte)(input[i] >> 2);
            if (i < 86)
            {
                BitArray bitsVolume = new BitArray(new byte[] { input[i] });
                var last2bits = (bitsVolume[0] ? 2 : 0) + (bitsVolume[1] ? 1 : 0);
                outputlast2[i] = (byte)(outputlast2[i] | last2bits);
            }
            else if (i < 172)
            {
                BitArray bitsVolume = new BitArray(new byte[] { input[i] });
                var last2bits = ((bitsVolume[0] ? 2 : 0) + (bitsVolume[1] ? 1 : 0)) << 2;
                outputlast2[i - 86] = (byte)(outputlast2[i - 86] | last2bits);
            }
            else
            {
                BitArray bitsVolume = new BitArray(new byte[] { input[i] });
                var last2bits = ((bitsVolume[0] ? 2 : 0) + (bitsVolume[1] ? 1 : 0)) << 4;
                outputlast2[i - 172] = (byte)(outputlast2[i - 172] | last2bits);
            }
        }

        var lastByte = 0;
        for (int i = 0; i < 86; i++)
        {
            outputlast2Encoded[i] = translateTable[(byte)(outputlast2[i] ^ lastByte)];
            lastByte = outputlast2[i];
            //Console.WriteLine("BC" + i.ToString("X2") + ": " + outputlast2[i].ToString("B8"));
        }

        List<byte> agregate = outputlast2Encoded.ToList();

        for (int i = 0; i < 256; i++)
        {
            outputDataEncoded[i] = translateTable[(byte)(outputData[i] ^ lastByte)];
            lastByte = outputData[i];
        }

        agregate.AddRange(outputDataEncoded.ToList());
        List<byte> checksum = new List<byte>() { translateTable[lastByte] };
        agregate.AddRange(checksum.ToList());

        return agregate.ToArray();
    }

    public byte detranlateTable(byte data)
    {
        for (int j = 0; j < translateTable.Length; j++)
        {
            if (translateTable[j] == data)
            {
                return (byte)j;
            }
        }
        return 0;
    }
    public byte[] Decode6_2(byte[] diskData)
    {
        byte[] dataTranslated = new byte[343];
        byte[] bufferData = new byte[343];


        byte[] inputlast2Encoded = new byte[0x56];
        byte[] inputDataEncoded = new byte[256];
        byte[] inputDataDecoded = new byte[256];
        byte[] outputlast2 = new byte[0x56];
        byte[] outputData = new byte[256];

        // Console.WriteLine("Nibblized 2 and 6: ");
        // Console.WriteLine();
        // for (int i = 0; i< diskData.Length-1; i++)
        // {
        //     if (i < 86)
        //     {
        //         inputlast2Encoded[i] = diskData[i];
        //         Console.Write(inputlast2Encoded[i].ToString("X2") + "|" + inputlast2Encoded[i].ToString("B8") + " ");
        //     }
        //     else 
        //     {
        //         if (i == 86)
        //         {
        //             Console.WriteLine(); 
        //             Console.WriteLine();
        //         }
        //         inputDataEncoded[i - 86] = diskData[i];
        //         Console.Write(inputDataEncoded[i - 86].ToString("X2") + "|" + inputDataEncoded[i - 86].ToString("B8") + " ");
        //     }
        // }

        // Console.WriteLine();
        // Console.WriteLine();
        byte prevByte = 0;
        for (int i = 0; i < diskData.Length; i++)
        {
            dataTranslated[i] = detranlateTable(diskData[i]);
            bufferData[i] = (byte)(dataTranslated[i] ^ prevByte);
            prevByte = bufferData[i];
        }


        for (int i = 0; i < bufferData.Length - 1; i++)
        {
            if (i < 86)
            {
                inputlast2Encoded[i] = bufferData[i];
                //Console.Write(inputlast2Encoded[i].ToString("X2") + "|" + inputlast2Encoded[i].ToString("B8") + " ");
            }
            else
            {
                // if (i == 86)
                // {
                //     Console.WriteLine(); 
                //     Console.WriteLine();
                // }
                inputDataEncoded[i - 86] = bufferData[i];
                //Console.Write(inputDataEncoded[i - 86].ToString("X2") + "|" + inputDataEncoded[i - 86].ToString("B8") + " ");
            }
        }



        for (int i = 0; i < inputDataEncoded.Length; i++)
        {
            outputData[i] = (byte)(inputDataEncoded[i] << 2);

        }

        for (int i = 0; i < outputData.Length; i++)
        {
            if (i < 86)
            {
                BitArray bitsVolume = new BitArray(new byte[] { inputlast2Encoded[i] });
                inputDataDecoded[i] = (byte)(outputData[i] + (bitsVolume[0] ? 2 : 0) + (bitsVolume[1] ? 1 : 0));
            }
            else if (i < 172)
            {
                BitArray bitsVolume = new BitArray(new byte[] { inputlast2Encoded[i - 86] });
                inputDataDecoded[i] = (byte)(outputData[i] + (bitsVolume[2] ? 2 : 0) + (bitsVolume[3] ? 1 : 0));
            }
            else
            {
                BitArray bitsVolume = new BitArray(new byte[] { inputlast2Encoded[i - 172] });
                inputDataDecoded[i] = (byte)(outputData[i] + (bitsVolume[4] ? 2 : 0) + (bitsVolume[5] ? 1 : 0));
            }
        }


        return inputDataDecoded;
    }

    public void TrackRawData(int track, bool update = false)
    {
        if (diskRawData[track] == null || update)
        {
            List<byte> selectedSector = new List<byte>();

            foreach (byte isec in new byte[] { 0xa, 0xb, 0xc, 0xd, 0xe, 0xf, 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9 })
            {
                List<byte> b = new List<byte>();
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss.ff") + " Load Track: " + track + " Sector: " + isec);
                selectedSector.AddRange(new List<byte>() { 0xff, 0xff, 0xff });
                selectedSector.AddRange(new List<byte>() { 0xd5, 0xaa, 0x96 }); // Prologe address
                var volume = memory.drive1.GetVolume();
                b = this.EncodeByte(volume).ToList();
                selectedSector.AddRange(b); // Volume
                b = this.EncodeByte((byte)track).ToList();
                selectedSector.AddRange(b); // Track
                b = this.EncodeByte(isec).ToList();
                selectedSector.AddRange(b); // Sector
                b = this.Checksum(volume, (byte)track, isec).ToList();
                selectedSector.AddRange(b); // Checksum
                selectedSector.AddRange(new List<byte>() { 0xde, 0xaa, 0xeb }); // Epilogue address
                selectedSector.AddRange(new List<byte>() { 0xd5, 0xaa, 0xad }); // Prologe data
                b = this.Encode6_2(track, this.translateDos33Track[isec]).ToList(); // DOS
                //b = this.Encode6_2(track, isec).ToList(); // PRODOS
                selectedSector.AddRange(b); // Data field + checksum
                selectedSector.AddRange(new List<byte>() { 0xde, 0xaa, 0xeb }); // Epilogue
                
            }
            Console.WriteLine(Print(selectedSector));
            diskRawData[track] = selectedSector.ToArray();
        }
    }


    public int GetOffset(int track, int sector)
    {
        return (sector * 256) + (track * (256 * 16));
    }

    public int GetInt16(byte[] data, int offset)
    {
        return (data[offset]) | ((data[offset + 1]) << 8);
    }

    string Print(List<byte> bytes)
    {
        string ret = "";
        for (int i = 0; i < bytes.Count; i = i + 16)
        {
            ret += i.ToString("X4") + ": ";
            foreach (byte b in bytes.Skip(i).Take(16))
            {
                ret += b.ToString("X2") + " ";
            }
            // foreach (byte b in bytes.Skip(i).Take(16))
            // {
            //     ret += Convert.ToChar((byte)(b - 0x80));
            // }
            ret += "\r\n";
        }
        return ret;
    }
}
