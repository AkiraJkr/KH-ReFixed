/*
==================================================
      KINGDOM HEARTS - RE:FIXED FOR BBS!
       COPYRIGHT TOPAZ WHITELOCK - 2022
 LICENSED UNDER DBAD. GIVE CREDIT WHERE IT'S DUE! 
==================================================
*/

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using System.Windows.Forms;

using AxaFormBase;
using ReFixed.Forms;

using DiscordRPC;

namespace ReFixed
{
    public class Functions
    {       
        /*
            Variable Space!
          
            Yes, this class has one, too!
        */

        static bool[] DEBOUNCE = new bool[] { false, false, false, false, false };

        static bool SAVE_ONCE;

        static byte SAVE_ROOM;
        static byte SAVE_WORLD;
        static byte SAVE_ITERATOR;

        static bool ALLOW_SAVE;
        static byte FRAME_ITERATOR;

        static byte LANGUAGE = 0xFF;

        /*
            Initialization:

            Serves only to unlock memory regions for now.
        */
        public static void Initialization()
        {
            Helpers.Log("Initializing Re:Fixed...", 0);

            if (!Directory.Exists(Path.GetTempPath() + "ReFixed"))
                Directory.CreateDirectory(Path.GetTempPath() + "ReFixed");
                
            if (!File.Exists(Variables.ToggleSFXPath))
            {
                var _saveStream = File.Create(Variables.SaveSFXPath);
                var _toggleStream = File.Create(Variables.ToggleSFXPath);

                Variables.SaveSFX.CopyTo(_saveStream);
                Variables.ToggleSFX.CopyTo(_toggleStream);
            }

            Hypervisor.UnlockBlock(Hypervisor.PureAddress + Variables.ADDR_LimiterINST, true);
            Hypervisor.UnlockBlock(Variables.ADDR_VoicePath);

            var _documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var _saveDir = Path.Combine(_documentsPath, "Kingdom Hearts/Save Data/");

            EPIC_INIT:
            if (Directory.Exists(_saveDir))
            {
                string[] _epicDirs = Directory.GetDirectories(_saveDir, "*", SearchOption.TopDirectoryOnly);

                if (_epicDirs.Length == 0x00)
                goto EPIC_INIT; 

                foreach (var _str in _epicDirs)
                {
                    var _folderName = new DirectoryInfo(_str).Name;
                    Directory.CreateDirectory(Path.Combine(_documentsPath, "Kingdom Hearts/Configuration/" + _folderName));

                    Helpers.Log("Detected and Created directories for ID: " + _folderName, 0);
                }
            }

            else
                goto EPIC_INIT;

            Variables.Source = new CancellationTokenSource();
            Variables.Token = Variables.Source.Token;

            Variables.Initialized = true;

            Helpers.Log("Re:Fixed initialized with no errors!", 0);
        }

        /*
            CheckTitle:

            Checks certain points in RAM to see if the player is in the Title Screen.
            Returns **true** if so, returns **false** otherwise. 
        */
        public static bool CheckTitle() =>
            Hypervisor.Read<byte>(Variables.ADDR_World) == 0xFF
            || Hypervisor.Read<byte>(Variables.ADDR_World) == 0x00
            || Hypervisor.Read<byte>(Variables.ADDR_World + 0x01) == 0xFF
            || Hypervisor.Read<byte>(0x1098D02D) == 0x00;

        /*
            ResetGame:

            Triggers a soft-reset if the proper input is given.
            The input is sought in Execute().

            INPUT: L1 + R1 + START + SELECT.
        */
        public static void ResetGame()
        {
            var _inputRead = Hypervisor.Read<ushort>(Variables.ADDR_Input);

            if ((_inputRead & 0x0C09) == 0x0C09 && !CheckTitle() && !DEBOUNCE[1])
            {
                Helpers.Log("Initiating a Soft Reset.", 0);

                Hypervisor.Write<byte>(Variables.ADDR_Limiter + 0x0C, 0x01);
                DEBOUNCE[1] = true;
            }

            else if ((_inputRead & 0x0C09) != 0x0C09 && DEBOUNCE[1])
                DEBOUNCE[1] = false;
        }

        /*
            FixExit:

            I sorta kinda unknowningly broke the Exit function in KH.
            To fix this, this function exists.
        */
        public static void FixExit()
        {
            if (CheckTitle())
            {
                var _pointFirst = Hypervisor.Read<ulong>(Variables.PINT_TitleOption);

                if (_pointFirst != 0x00)
                {
                    var _pointIterate = Hypervisor.Read<ulong>(_pointFirst + 0x10, true);

                    _pointIterate = Hypervisor.Read<ulong>(_pointIterate + 0x50, true);
                    _pointIterate = Hypervisor.Read<ulong>(_pointIterate + 0x20, true);
                    _pointIterate = Hypervisor.Read<ulong>(_pointIterate + 0xC8, true);
                    _pointIterate = Hypervisor.Read<ulong>(_pointIterate + 0x28, true);
                    _pointIterate = Hypervisor.Read<ulong>(_pointIterate + 0x40, true);

                    var _selectButton = Hypervisor.Read<byte>(_pointIterate + 0x8E);
                    var _countButton = Hypervisor.Read<byte>(_pointIterate + 0x8E + 0x04);

                    var _inputRead = Hypervisor.Read<ushort>(Variables.ADDR_Input);
                    var _confirmRead = Hypervisor.Read<byte>(Variables.ADDR_Confirm);

                    var _buttonSeek = (_confirmRead == 0x01 ? 0x2000 : 0x4000);
                    var _inputValue = _inputRead & _buttonSeek;

                    if (_inputValue == _buttonSeek && _selectButton == _countButton - 0x01)
                    {
                        Helpers.Log("Title to Exit detected! 2.5 second limit set! Initating exit...", 0);
                        Thread.Sleep(2500);

                        if (File.Exists("KINGDOM HEARTS HD 1.5+2.5 Launcher.exe"))
                        {
                            Helpers.Log("Launcher found! Launching the launcher...", 0);
                            Process.Start("KINGDOM HEARTS HD 1.5+2.5 Launcher");
                        }
                        
                        Helpers.Log("Re:Fixed terminated with no errors.", 0);
                        Environment.Exit(0);
                    }
                }
            }
        }

        /*
            TextAdjust:
            Overwrite the text in certain portions of the game, to give the illusion that
            the features given are Square-made, and not some jank being made by a 20-year-old
            no life :^)
        */
        public static void TextAdjust()
        {
            var _basePointer = Hypervisor.Read<ulong>(Variables.PINT_SettingsText);

            var _headerBegin = _basePointer;

            var _textBegin = _basePointer + 0x1121;
            var _textEnd = _basePointer + 0x3670;

            var _headerMagic = Hypervisor.ReadArray(_headerBegin, 0x04, true);
            var _headerBlock = Hypervisor.ReadArray(_headerBegin, 0x9A0, true);

            var _optionTitle = _headerBlock.FindValue(0x3432030F) + 0x04;

            var _optionNoDesc = _headerBlock.FindValue(0x34322601) + 0x04;
            var _optionYesDesc = _headerBlock.FindValue(0x34322600) + 0x04;

            var _optionNo = _headerBlock.FindValue(0x34320B00) + 0x04;    
            var _optionYes = _headerBlock.FindValue(0x34320B01) + 0x04;

            var _yesDescVal = Hypervisor.Read<ushort>(_headerBegin + _optionYesDesc, true);
            var _noDescVal = Hypervisor.Read<ushort>(_headerBegin + _optionNoDesc, true);

            if (_headerMagic.SequenceEqual(new byte[] { 0x40, 0x43, 0x54, 0x44 }))
            {
                var _yesLocation = Hypervisor.Read<ushort>(_headerBegin + _optionYes, true);

                if (Variables.DualAudio && _optionNoDesc != _optionYesDesc)
                {
                    Hypervisor.Write<ushort>(_headerBegin + _optionNoDesc, _yesDescVal, true);

                    _optionNoDesc = _optionYesDesc;
                }

                var _locArray = new ulong[]
                {
                    _optionTitle,
                    _optionYes,
                    _optionNo,
                    _optionYesDesc,
                    _optionNoDesc
                };

                if (LANGUAGE == 0xFF)
                {
                    var _yesRead = Hypervisor.ReadTerminate(_headerBegin + _yesLocation, true);
                    var _catchStr = Strings.OriginText.FirstOrDefault(x => x == _yesRead);
                    LANGUAGE = (byte)Array.IndexOf(Strings.OriginText, _catchStr);
                }

                else
                {
                    var _strArr = Variables.DualAudio ? Strings.DualAudio[LANGUAGE] : Strings.AutoSave[LANGUAGE];

                    for (int i = 0; i < _strArr.Length; i++)
                    {
                        if (!Variables.DualAudio && i == Strings.AutoSave[LANGUAGE].Length - 1)
                        {
                            var _strLength = Hypervisor.Read<int>(_headerBegin + _optionYesDesc, true) + (Variables.DualAudio ? Strings.DualAudio[LANGUAGE][i - 1].Length : Strings.AutoSave[LANGUAGE][i - 1].Length) + 0x01;
                            var _noRead = Hypervisor.Read<int>(_headerBegin + _optionNoDesc, true);

                            if (_noRead != _strLength)
                            {
                                Hypervisor.Write<int>(_headerBegin + _optionNoDesc, _strLength, true);
                                Hypervisor.WriteString(_headerBegin + (ulong)_strLength, _str, true);
                            }
                        }
                        
                        Hypervisor.WriteString(_headerBegin + Hypervisor.Read<uint>(_headerBegin + _locArray[i], true), _strArr[i], true);
                    }
                }
            }
        }

        /*
            FinisherPrompt:

            Allow finishers to be renamed through a pop-up window, bound to Triangle.
        */
        public static void FinisherPrompt()
        {
            // Fetch the Status Menu pointer.
            var _statusPointer = Hypervisor.Read<ulong>(Variables.PINT_StatusMenu);
            var _commandPointer = Hypervisor.Read<ulong>(Variables.PINT_CommandMenu);

            // If the Status Menu is open, and it's pointer fetched:
            if (_statusPointer > 0 || _commandPointer > 0)
            {
                // Fetch the Finisher Menu pointer.
                var _finishPointer =
                    _statusPointer > 0
                        ? Hypervisor.Read<ulong>(_statusPointer + 0xC8, true)
                        : Hypervisor.Read<ulong>(_commandPointer + 0xF0, true);

                // If the Finisher Menu is open, and it's pointer fetched:
                if (_finishPointer > 0)
                {
                    // Read the input, and the finisher that is currently selected.
                    // "Selected" means "Hovering", not "Active".
                    var _inputRead = Hypervisor.Read<ushort>(Variables.ADDR_Input);

                    var _selectedFinisher =
                        _statusPointer > 0
                            ? Hypervisor.Read<byte>(_finishPointer + 0x8E, true)
                            : Hypervisor.Read<ulong>(_finishPointer + 0xD8, true);

                    var _isFinisher =  _statusPointer > 0 ? true : (Hypervisor.Read<byte>(_selectedFinisher + 0x92, true) > 0x00 && Hypervisor.Read<byte>(_selectedFinisher + 0x8C, true) == 0x00);  
                    _isFinisher = _selectedFinisher > 0 ? _isFinisher : false;

                    _selectedFinisher =
                        _statusPointer > 0
                            ? _selectedFinisher
                            : Hypervisor.Read<byte>(_selectedFinisher + 0x8E, true);

                    // If the debounce is not active, and Triangle is pressed:
                    if ((_inputRead & 0x1000) == 0x1000 && !DEBOUNCE[0] && _isFinisher)
                    {
                        // Activate debounce.
                        DEBOUNCE[0] = true;

                        // Play a sound, dictating that you actually pressed a button!
                        Helpers.PlaySFX(Variables.ToggleSFXPath);

                        // Using the input form we made specifically for this:
                        using (InputText _inForm = new InputText())
                        {
                            Helpers.Log("Showing the form for Finisher Renames.", 0);

                            // Release the mouse.
                            BaseSimpleForm.CaptureStatus = false;

                            // Literally halt the game.
                            BaseSimpleForm.theInstance.suspend();

                            // Set the form position to be the center of the game form, and show the dialog.
                            _inForm.StartPosition = FormStartPosition.CenterParent;
                            var _result = _inForm.ShowDialog(BaseSimpleForm.theInstance);

                            // If the result of said dialog is OK:
                            if (_result == DialogResult.OK)
                            {
                                Helpers.Log("Renaming the chosen finisher to: \"" + _inForm.FinisherName + "\"!", 0);

                                // Fetch the text and turn it to a byte array.
                                var _textArray = Encoding.ASCII.GetBytes(_inForm.FinisherName);

                                // Make an array that will be 0x10 bytes long when finished.
                                var _fillerArray = new List<byte>();

                                // Add the text to the new array, then fill it out to be 0x10 in size.
                                _fillerArray.AddRange(_textArray);
                                _fillerArray.AddRange(new byte[0x10 - _textArray.Length]);

                                // Write the array to the chosen finisher.
                                Hypervisor.WriteArray(
                                    Variables.ADDR_FinisherName + (ulong)(0x26 * _selectedFinisher),
                                    _fillerArray.ToArray()
                                );
                            }

                            // Capture the mouse.
                            BaseSimpleForm.CaptureStatus = true;

                            // Resume the game.
                            BaseSimpleForm.theInstance.resume();
                        }
                    }
                    
                    // Otherwise, if debounce is active and Triangle is NOT pressed:
                    // Deactivate debounce.
                    else if ((_inputRead & 0x1000) != 0x1000 && DEBOUNCE[0])
                        DEBOUNCE[0] = false;
                }
            }
        }

        /*
            FrameOverride:

            Overwrites the frame limiter, and the instruction forcing it, according
            to the framerate chosen by the player.

            So, the same sort of shit as KH2?
            Exactly!
        */
        public static void FrameOverride()
        {
            var _nullArray = new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };

            // Calculate the instruction address.
            var _instructionAddress = Hypervisor.PureAddress + Variables.ADDR_LimiterINST;

            // Fetch the framerate, and the first byte of the instruction.
            var _framerateRead = Hypervisor.Read<byte>(Variables.ADDR_Framerate);
            var _instructionRead = Hypervisor.Read<byte>(_instructionAddress, true);

            // If the framerate is set to 30FPS, and the limiter is NOP'd out: Rewrite the instruction.
            if (_framerateRead == 0x00 && _instructionRead == 0x90)
            {
                Helpers.Log("30FPS Detected! Restoring the Framelimiter.", 0);
                Hypervisor.WriteArray(_instructionAddress, Variables.INST_FrameLimiter, true);
            }

            // Otherwise, if the framerate is not set to 30FPS, and the limiter is present:
            else if (_framerateRead != 0x00 && _instructionRead != 0x90)
            {
                Helpers.Log("60FPS Detected! Destroying the Framelimiter.", 0);

                // NOP the instruction.
                Hypervisor.WriteArray(_instructionAddress, _nullArray, true);

                // Set the current limiter to be off.
                Hypervisor.Write<byte>(Variables.ADDR_Limiter, 0x00);
            }
        }

        /*
            AudioSwap:

            Enforces English or Japanese Audio depending on player preference.
            This is detemined by the **Vibration** option at the Camp Menu.

            This function is reliant on a patch.
        */
        public static void AudioSwap()
        {
            if (Hypervisor.Read<byte>(Variables.ADDR_Config) == 0x00)
                Hypervisor.WriteString(Variables.ADDR_VoicePath, "jp");

            else
                Hypervisor.WriteString(Variables.ADDR_VoicePath, "en");
        }

        /*
            GenerateSave:

            Only to be triggered by AutosaveEngine(), generate and write a save to
            both RAM and ROM portions, effectively saving the game.
        */
        public static void GenerateSave()
        {
            if (SAVE_ONCE)
            {
                // Prepare the pointers.
                var _pointerBase = Hypervisor.Read<ulong>(Variables.PINT_SaveInformation);
                var _pointerSecond = Hypervisor.Read<ulong>(_pointerBase + 0x10, true);

                // Prepare the strings.
                var _saveName = "XB1-BBS-99";
                var _savePath = Hypervisor.ReadTerminate(_pointerBase + 0x40, true) + "\\KHBbSFM.png";

                // Calculate the Unix Date.
                var _currDate = DateTime.Now;
                var _unix = new DateTime(1970, 1, 1);
                var _writeDate = Convert.ToUInt64((_currDate - _unix).TotalSeconds);

                // Prepare the variables for Save Info.
                var _saveSlot = 0;
                var _saveInfoLength = 0x158;
                var _saveDataLength = 0x13C00;

                var _saveInfoStartRAM = _pointerSecond + 0x168;
                var _saveDataStartRAM = _pointerSecond + 0x1C270;

                var _saveInfoStartFILE = 0x1C8;
                var _saveDataStartFILE = 0x1C2D0;

                // Read the save from RAM.
                var _saveData = Hypervisor.ReadArray(Variables.ADDR_SaveData, 0x11E50);

                // Read the save slot.
                var _saveSlotRAM = Hypervisor.ReadArray(
                    _saveInfoStartRAM + (ulong)(_saveInfoLength * _saveSlot),
                    0x11,
                    true
                );

                // If the file does not bear a save; terminate the operation.
                if (!Encoding.Default.GetString(_saveSlotRAM).Contains("BBS"))
                    return;

                // Seek out the physical slot of the save to make.
                while (
                    _saveSlotRAM[0] != 0x00
                    && !Encoding.Default.GetString(_saveSlotRAM).Contains("BBS-99")
                )
                {
                    _saveSlot++;
                    _saveSlotRAM = Hypervisor.ReadArray(
                        _saveInfoStartRAM + (ulong)(_saveInfoLength * _saveSlot),
                        0x11,
                        true
                    );
                }

                // Calculate the checksums.
                var _checkData = Extensions.CalculateCRC32(new MemoryStream(_saveData));

                #region RAM Save
                // Fetch the address for the save info.
                var _saveInfoAddrRAM = _saveInfoStartRAM + (ulong)(_saveInfoLength * _saveSlot);
                var _saveDataAddrRAM = _saveDataStartRAM + (ulong)(_saveDataLength * _saveSlot);

                // Write out the save information.
                Hypervisor.WriteArray(_saveInfoAddrRAM, Encoding.Default.GetBytes(_saveName), true);

                // Write the date in which the save was made.
                Hypervisor.Write<ulong>(_saveInfoAddrRAM + 0x40, _writeDate, true);
                Hypervisor.Write<ulong>(_saveInfoAddrRAM + 0x48, _writeDate, true);

                // Write the length of the save.
                Hypervisor.Write<int>(_saveInfoAddrRAM + 0x50, _saveDataLength, true);

                // Write the header.
                Hypervisor.WriteArray(_saveDataAddrRAM, Encoding.Default.GetBytes("BBSD"), true);
                Hypervisor.Write<uint>(_saveDataAddrRAM + 0x04, 0x1D, true);

                // Write the size.
                Hypervisor.Write<int>(_saveDataAddrRAM + 0x08, 0x12000, true);

                // Write the checksum.
                Hypervisor.Write<uint>(_saveDataAddrRAM + 0x0C, _checkData, true);

                // Write, the save.
                Hypervisor.WriteArray(_saveDataAddrRAM + 0x10, _saveData.Skip(0x10).ToArray(), true);
                #endregion

                #region File Save

                // Fetch the address for the save info.
                var _saveInfoAddr = _saveInfoStartFILE + _saveInfoLength * _saveSlot;
                var _saveDataAddr = _saveDataStartFILE + _saveDataLength * _saveSlot;

                // Create the writer.
                using (var _stream = new FileStream(_savePath, FileMode.Open))
                using (var _write = new BinaryWriter(_stream))
                {
                    // Write out the save information.
                    _stream.Position = _saveInfoAddr;
                    _write.Write(Encoding.Default.GetBytes(_saveName));

                    // The date in which the save was made.
                    _stream.Position = _saveInfoAddr + 0x40;
                    _write.Write(_writeDate);
                    _stream.Position = _saveInfoAddr + 0x48;
                    _write.Write(_writeDate);

                    // The length of the save.
                    _stream.Position = _saveInfoAddr + 0x50;
                    _write.Write(0x12000);

                    // Write the header.
                    _stream.Position = _saveDataAddr;
                    _write.Write(Encoding.Default.GetBytes("BBSD"));
                    _stream.Position = _saveDataAddr + 0x04;
                    _write.Write(0x1D);

                    // Write the size.
                    _stream.Position = _saveDataAddr + 0x08;
                    _write.Write(0x11E50);

                    // Write the checksum.
                    _stream.Position = _saveDataAddr + 0x0C;
                    _write.Write(_checkData);

                    // Write, the save.
                    _stream.Position = _saveDataAddr + 0x10;
                    _write.Write(_saveData.Skip(0x10).ToArray());
                }
                #endregion

                // Play a sound, dictating that the save was a success!
                if (Variables.sfxToggle)
                    Helpers.PlaySFX(Variables.SaveSFXPath);
            }
            
            else
                SAVE_ONCE = true;
        }

        /*
            AutosaveEngine:

            As the name suggests, handle the logic behind Autosave functionality.
        */
        public static void AutosaveEngine()
        {
            var _settingCheck = Hypervisor.Read<byte>(Variables.ADDR_Config);
            var _saveableBool = Variables.DualAudio ? Variables.saveToggle : _settingCheck == 0x01;

            if (_saveableBool && !CheckTitle())
            {
                var _worldCheck = Hypervisor.Read<byte>(Variables.ADDR_World);
                var _roomCheck = Hypervisor.Read<byte>(Variables.ADDR_World + 0x01);

                var _loadWorld = Hypervisor.Read<byte>(Variables.ADDR_LoadWorld);
                var _loadCheck = Hypervisor.Read<byte>(Variables.ADDR_LoadFlag);

                var _cutsceneCheck = Hypervisor.Read<byte>(Variables.ADDR_CutsceneFlag);
                var _battleCheck = Hypervisor.Read<byte>(Variables.ADDR_BattleFlag);
                var _hudCheck = Hypervisor.Read<byte>(Variables.ADDR_HUDFlag);

                var _isViable = (_battleCheck == 0x00 && _hudCheck == 0xC0) && _loadCheck == 0x01 && _loadWorld == 0x00 && _cutsceneCheck == 0x00;

                if (_isViable)
                {
                    if (SAVE_WORLD != _worldCheck)
                    {
                        Helpers.Log("World condition met! Writing Autosave...", 0);

                        GenerateSave();
                        SAVE_ITERATOR = 0;
                    }

                    else if (SAVE_ROOM != _roomCheck && _worldCheck >= 2)
                    {
                        SAVE_ITERATOR++;

                        if (SAVE_ITERATOR == 0x03)
                        {
                            Helpers.Log("Room condition met! Writing Autosave...", 0);

                            GenerateSave();
                            SAVE_ITERATOR = 0;
                        }
                    }
                                    
                    SAVE_ROOM = _roomCheck;
                    SAVE_WORLD = _worldCheck;
                }
            }
        }

        /*
            DiscordEngine:

            Handle the Discord Rich Presence of Re:Fixed.
            To be executed on a separate thread.
        */
        public static void DiscordEngine()
        {
            var _levelValue = Hypervisor.Read<byte>(0x1098D02D);
            var _diffValue = Hypervisor.Read<byte>(0x1097ADBD);
            var _charValue = Hypervisor.Read<byte>(0x1098CF98);

            var _stringDetail = string.Format(
                "Level {0} | {1} Mode",
                _levelValue,
                Variables.MDEDictionary.ElementAtOrDefault(_diffValue)
            );
            var _stringState = string.Format(
                "Character: {0}",
                Variables.CHRDictionary.ElementAtOrDefault(_charValue)
            );

            var _worldID = Hypervisor.Read<byte>(Variables.ADDR_World);
            var _battleFlag = Hypervisor.Read<byte>(Variables.ADDR_World);

            var _rpcButtons = new DiscordRPC.Button[]
            {
                new DiscordRPC.Button
                {
                    Label = "== Powered by Re:Fixed ==",
                    Url = "https://github.com/TopazTK/KH-ReFixed"
                },
                new DiscordRPC.Button
                {
                    Label = "== Icons by Televo ==",
                    Url = "https://github.com/Televo/kingdom-hearts-recollection"
                }
            };

            if (!CheckTitle())
            {
                Variables.DiscordClient.SetPresence(
                    new RichPresence
                    {
                        Details = _stringDetail,
                        State = _stringState,
                        Assets = new Assets
                        {
                            LargeImageKey = Variables.WRLDictionary.ElementAtOrDefault(_worldID),
                            SmallImageKey = _battleFlag % 2 == 0 ? "safe" : "battle",
                            SmallImageText = _battleFlag % 2 == 0 ? "Safe" : "In Battle"
                        },
                        Buttons = _rpcButtons
                    }
                );
            }
            else
            {
                Variables.DiscordClient.SetPresence(
                    new RichPresence
                    {
                        Details = "On the Title Screen",
                        State = null,
                        Assets = new Assets
                        {
                            LargeImageKey = "title",
                            SmallImageKey = null,
                            SmallImageText = null
                        },
                        Buttons = _rpcButtons
                    }
                );
            }
        }

        /*
            Execute:

            Executes the main logic within Re:Fixed.
        */
        public static void Execute()
        {
            try
            {
                #region High Priority
                if (!Variables.Initialized)
                    Initialization();

                ResetGame();
                FixExit();
                #endregion

                #region Mid Priority
                if (Variables.DualAudio)
                    AudioSwap();

                TextAdjust();
                FinisherPrompt();
                FrameOverride();
                #endregion

                #region Low Priority
                #endregion

                #region Tasks
                if (Variables.ASTask == null)
                {
                    Variables.ASTask = Task.Factory.StartNew(
                        delegate()
                        {
                            while (!Variables.Token.IsCancellationRequested)
                            {
                                AutosaveEngine();
                                Thread.Sleep(5);
                            }
                        },
                        Variables.Token
                    );
                }

                if (Variables.DCTask == null && Variables.rpcToggle)
                {
                    Variables.DCTask = Task.Factory.StartNew(
                        delegate()
                        {
                            while (!Variables.Token.IsCancellationRequested)
                            {
                                DiscordEngine();
                                Thread.Sleep(5);
                            }
                        },
                        Variables.Token
                    );
                }
                #endregion
            }
            
            catch (Exception _caughtEx)
            {
                Helpers.LogException(_caughtEx);
                Helpers.Log("Re:Fixed terminated with an exception!", 1);
                Environment.Exit(-1);
            }
        }
    }
}
