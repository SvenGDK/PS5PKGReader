using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace PS5PKGReader
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private List<PS5PKGRootFile> PFSImageRootFiles = [];
        private List<PS5PKGRootDirectory> PFSImageRootDirectories = [];
        private List<PS5PKGRootFile> PFSImageURootFiles = [];
        private List<PS5PKGRootFile> NestedImageRootFiles = [];
        private List<PS5PKGRootDirectory> NestedImageRootDirectories = [];
        private List<PS5PKGRootFile> NestedImageURootFiles = [];

        private bool IsSourcePKG = false;
        private bool IsRetailPKG = false;

        private string CurrentParamJSON = "";
        private XDocument? CurrentConfigurationXML = null;
        private Bitmap? CurrentIcon0 = null;
        private Bitmap? CurrentPic0 = null;

        private async void BrowsePKGFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (VisualRoot is not Window window)
                return;

            var newOpenFileDialog = new OpenFileDialog() { Title = "Select a PS5 PKG file :", AllowMultiple = false, Filters = [new FileDialogFilter() { Name = "PKG files", Extensions = { "pkg" } }] };
            var selectedFile = await newOpenFileDialog.ShowAsync(window);

            if (selectedFile is null || selectedFile.Length == 0)
                return;
            if (selectedFile[0] is not null)
            {
                SelectedPKGFileTextBox.Text = selectedFile[0];

                // Clear previous ListBox items and lists
                PKGContentListBox.Items.Clear();
                PKGScenariosListBox.Items.Clear();
                PKGChunksListBox.Items.Clear();
                PKGOutersListBox.Items.Clear();

                PFSImageRootFiles.Clear();
                PFSImageRootDirectories.Clear();
                PFSImageURootFiles.Clear();
                NestedImageRootFiles.Clear();
                NestedImageRootDirectories.Clear();
                NestedImageURootFiles.Clear();

                // Reset
                PKGIconImage.Source = null;
                IsSourcePKG = false;
                IsRetailPKG = false;
                CurrentParamJSON = "";
                CurrentConfigurationXML = null;
                CurrentIcon0 = null;
                CurrentPic0 = null;

                // Determine PS5 PKG
                string FirstString = "";
                sbyte Int8AtOffset5;
                using (var PKGReader = new FileStream(selectedFile[0], FileMode.Open, FileAccess.Read))
                {
                    var BinReader = new BinaryReader(PKGReader);
                    FirstString = BinReader.ReadString();
                    PKGReader.Seek(5L, SeekOrigin.Begin);
                    Int8AtOffset5 = BinReader.ReadSByte();
                    PKGReader.Close();
                }

                if (!string.IsNullOrEmpty(FirstString))
                {
                    if (FirstString.Contains("CNT"))
                    {
                        IsSourcePKG = true;
                    }
                    else
                    {
                        IsSourcePKG = false;
                    }
                }

                if (Int8AtOffset5 == -128)
                {
                    IsRetailPKG = true;
                }

                if (IsRetailPKG | IsSourcePKG)
                {
                    // Get only param.json
                    byte[] startBytes = Encoding.UTF8.GetBytes("param.json");
                    byte[] endBytes = Encoding.UTF8.GetBytes("version.xml");

                    long startOffset = -1;
                    long endOffset = -1;

                    using (var PKGReader = new FileStream(selectedFile[0], FileMode.Open, FileAccess.Read))
                    {
                        var buffer = new byte[4097];
                        long fileLength = PKGReader.Length;
                        long totalBytesRead = fileLength;

                        while (totalBytesRead > 0L)
                        {
                            int bytesRead = (int)Math.Min(buffer.Length, totalBytesRead);
                            PKGReader.Seek(totalBytesRead - bytesRead, SeekOrigin.Begin);
                            PKGReader.Read(buffer, 0, bytesRead);
                            totalBytesRead -= bytesRead;

                            bool exitWhile = false;
                            for (int i = bytesRead - 1; i >= 0; i -= 1)
                            {
                                if (endOffset == -1 && MatchBytes(buffer, i, endBytes))
                                {
                                    endOffset = totalBytesRead + i + endBytes.Length;
                                }

                                if (startOffset == -1 && MatchBytes(buffer, i, startBytes))
                                {
                                    startOffset = totalBytesRead + i;
                                }

                                if (startOffset != -1 && endOffset != -1)
                                {
                                    exitWhile = true;
                                    break;
                                }
                            }

                            if (exitWhile)
                            {
                                break;
                            }
                        }
                    }

                    if (startOffset != -1 && endOffset != -1 && endOffset > startOffset)
                    {
                        string FinalParamJSONString = "";
                        using (var ParamJSONFileStream = new FileStream(selectedFile[0], FileMode.Open, FileAccess.Read))
                        {
                            long ParamDataSize = endOffset - startOffset;
                            ParamJSONFileStream.Seek(startOffset, SeekOrigin.Begin);

                            var NewParamData = new byte[((int)ParamDataSize)];
                            ParamJSONFileStream.Read(NewParamData, 0, (int)ParamDataSize);

                            string ExtractedData = Encoding.UTF8.GetString(NewParamData);
                            var ParamJSONData = ExtractedData.Split(new string[] { Constants.vbCrLf }, StringSplitOptions.None).ToList();

                            // Adjust the output
                            ParamJSONData.RemoveAt(0);
                            ParamJSONData.Insert(0, "{");
                            ParamJSONData[ParamJSONData.Count - 1] += "\"";
                            ParamJSONData.Add("}");

                            FinalParamJSONString = string.Join(Environment.NewLine, ParamJSONData);
                        }

                        if (!string.IsNullOrEmpty(FinalParamJSONString))
                        {
                            CurrentParamJSON = FinalParamJSONString;

                            // Display pkg information
                            var ParamData = JsonConvert.DeserializeObject<PS5ParamClass.PS5Param>(FinalParamJSONString);
                            var NewPS5Game = new PS5Game() { GameBackupType = "PKG" };
                            if (ParamData is not null)
                            {
                                if (ParamData.TitleId is not null)
                                {
                                    NewPS5Game.GameID = "Title ID: " + ParamData.TitleId;
                                    NewPS5Game.GameRegion = "Region: " + PS5Game.GetGameRegion(ParamData.TitleId);
                                }

                                if (ParamData.LocalizedParameters.EnUS is not null)
                                {
                                    NewPS5Game.GameTitle = ParamData.LocalizedParameters.EnUS.TitleName;
                                }
                                if (ParamData.LocalizedParameters.DeDE is not null)
                                {
                                    NewPS5Game.DEGameTitle = ParamData.LocalizedParameters.DeDE.TitleName;
                                }
                                if (ParamData.LocalizedParameters.FrFR is not null)
                                {
                                    NewPS5Game.FRGameTitle = ParamData.LocalizedParameters.FrFR.TitleName;
                                }
                                if (ParamData.LocalizedParameters.ItIT is not null)
                                {
                                    NewPS5Game.ITGameTitle = ParamData.LocalizedParameters.ItIT.TitleName;
                                }
                                if (ParamData.LocalizedParameters.EsES is not null)
                                {
                                    NewPS5Game.ESGameTitle = ParamData.LocalizedParameters.EsES.TitleName;
                                }
                                if (ParamData.LocalizedParameters.JaJP is not null)
                                {
                                    NewPS5Game.JPGameTitle = ParamData.LocalizedParameters.JaJP.TitleName;
                                }

                                if (ParamData.ContentId is not null)
                                {
                                    NewPS5Game.GameContentID = "Content ID: " + ParamData.ContentId;
                                }

                                if (ParamData.ApplicationCategoryType == 0)
                                {
                                    NewPS5Game.GameCategory = "Type: PS5 Game";
                                }
                                else if (ParamData.ApplicationCategoryType == 65792)
                                {
                                    NewPS5Game.GameCategory = "Type: RNPS Media App";
                                }
                                else if (ParamData.ApplicationCategoryType == 131328)
                                {
                                    NewPS5Game.GameCategory = "Type: System Built-in App";
                                }
                                else if (ParamData.ApplicationCategoryType == 131584)
                                {
                                    NewPS5Game.GameCategory = "Type: Big Daemon";
                                }
                                else if (ParamData.ApplicationCategoryType == 16777216)
                                {
                                    NewPS5Game.GameCategory = "Type: ShellUI";
                                }
                                else if (ParamData.ApplicationCategoryType == 33554432)
                                {
                                    NewPS5Game.GameCategory = "Type: Daemon";
                                }
                                else if (ParamData.ApplicationCategoryType == 67108864)
                                {
                                    NewPS5Game.GameCategory = "Type: ShellApp";
                                }

                                NewPS5Game.GameSize = "Size: " + Strings.FormatNumber(new FileInfo(selectedFile[0]).Length / 1073741824d, 2) + " GB";

                                if (ParamData.ContentVersion is not null)
                                {
                                    NewPS5Game.GameVersion = "Version: " + ParamData.ContentVersion;
                                }
                                if (ParamData.RequiredSystemSoftwareVersion is not null)
                                {
                                    NewPS5Game.GameRequiredFirmware = "Required Firmware: " + ParamData.RequiredSystemSoftwareVersion.Replace("0x", "").Insert(2, ".").Insert(5, ".").Insert(8, ".").Remove(11, 8);
                                }

                                GameTitleTextBlock.IsVisible = true;
                                GameIDTextBlock.IsVisible = true;
                                GameRegionTextBlock.IsVisible = true;
                                GameVersionTextBlock.IsVisible = true;
                                GameContentIDTextBlock.IsVisible = true;
                                GameCategoryTextBlock.IsVisible = true;
                                GameSizeTextBlock.IsVisible = true;
                                GameRequiredFirmwareTextBlock.IsVisible = true;

                                GameTitleTextBlock.Text = NewPS5Game.GameTitle;
                                GameIDTextBlock.Text = NewPS5Game.GameID;
                                GameRegionTextBlock.Text = NewPS5Game.GameRegion;
                                GameVersionTextBlock.Text = NewPS5Game.GameVersion;
                                GameContentIDTextBlock.Text = NewPS5Game.GameContentID;
                                GameCategoryTextBlock.Text = NewPS5Game.GameCategory;
                                GameSizeTextBlock.Text = NewPS5Game.GameSize;
                                GameRequiredFirmwareTextBlock.Text = NewPS5Game.GameRequiredFirmware;
                            }
                        }

                    }

                    return;
                }

                // Probably a self created PKG that contains a package configuration
                string ExtractedPKGConfigurationData = "";
                string PKGConfigurationStartString = "<package-configuration version=\"1.0\" type=\"package-info\">";
                string PKGConfigurationEndString = "</package-configuration>";

                byte[] PKGConfigurationStartBytes = Encoding.UTF8.GetBytes(PKGConfigurationStartString);
                byte[] PKGConfigurationEndBytes = Encoding.UTF8.GetBytes(PKGConfigurationEndString);

                long PKGConfigurationStartOffset = -1;
                long PKGConfigurationEndOffset = -1;

                // 1. Get the PKG configuration
                using (var PKGReader = new FileStream(selectedFile[0], FileMode.Open, FileAccess.Read))
                {
                    var buffer = new byte[4097];
                    long fileLength = PKGReader.Length;
                    long totalBytesRead = fileLength;

                    // Read backwards to find the end string first
                    while (totalBytesRead > 0L)
                    {
                        int bytesRead = (int)Math.Min(buffer.Length, totalBytesRead);
                        PKGReader.Seek(totalBytesRead - bytesRead, SeekOrigin.Begin);
                        PKGReader.Read(buffer, 0, bytesRead);
                        totalBytesRead -= bytesRead;

                        // Check buffer from end to start
                        bool exitWhile1 = false;
                        for (int i = bytesRead - 1; i >= 0; i -= 1)
                        {
                            // Check for end string
                            if (PKGConfigurationEndOffset == -1 && MatchBytes(buffer, i, PKGConfigurationEndBytes))
                            {
                                PKGConfigurationEndOffset = totalBytesRead + i + PKGConfigurationEndBytes.Length;
                            }

                            // Check for start string
                            if (PKGConfigurationStartOffset == -1 && MatchBytes(buffer, i, PKGConfigurationStartBytes))
                            {
                                PKGConfigurationStartOffset = totalBytesRead + i;
                            }

                            if (PKGConfigurationStartOffset != -1 && PKGConfigurationEndOffset != -1)
                            {
                                exitWhile1 = true;
                                break;
                            }
                        }

                        if (exitWhile1)
                        {
                            break;
                        }
                    }

                    if (PKGConfigurationStartOffset != -1 & PKGConfigurationEndOffset != -1 && PKGConfigurationEndOffset > PKGConfigurationStartOffset)
                    {
                        PKGConfigurationStartOffset -= PKGConfigurationStartBytes.Length;
                        PKGConfigurationEndOffset -= PKGConfigurationEndBytes.Length - 1;

                        // Calculate the size of the pkg configuration data
                        long PKGConfigurationDataSize = PKGConfigurationEndOffset - PKGConfigurationStartOffset;

                        // Move to the start offset
                        PKGReader.Seek(PKGConfigurationStartOffset, SeekOrigin.Begin);

                        // Read the pkg configuration data
                        var data = new byte[((int)PKGConfigurationDataSize)];
                        PKGReader.Read(data, 0, (int)PKGConfigurationDataSize);

                        // Convert the data to a XML string
                        ExtractedPKGConfigurationData = Encoding.UTF8.GetString(data);
                        ExtractedPKGConfigurationData = string.Concat("<?xml version=\"1.0\" encoding=\"utf-8\"?>", ExtractedPKGConfigurationData);
                    }
                }

                // 2. Process the retrieved PKG configuration data
                if (!string.IsNullOrEmpty(ExtractedPKGConfigurationData))
                {
                    // Load the XML file
                    XDocument PKGConfigurationXML = XDocument.Parse(ExtractedPKGConfigurationData);
                    CurrentConfigurationXML = PKGConfigurationXML;

                    if (PKGConfigurationXML != null) {
                        // Get the PKG config values
                        var PKGConfig = PKGConfigurationXML.Element("config");
                        if (PKGConfig is not null)
                        {
                            string PKGConfigVersion = PKGConfig.Attribute("version")?.Value;
                            string PKGConfigMetadata = PKGConfig.Attribute("metadata")?.Value;
                            string PKGConfigPrimary = PKGConfig.Attribute("primary")?.Value;
                        }
                        string PKGConfigContentID = PKGConfigurationXML.Descendants("config").First().Element("content-id").Value;
                        string PKGConfigPrimaryID = PKGConfigurationXML.Descendants("config").First().Element("primary-id").Value;
                        string PKGConfigLongName = PKGConfigurationXML.Descendants("config").First().Element("longname").Value;
                        string PKGConfigRequiredSystemVersion = PKGConfigurationXML.Descendants("config").First().Element("required-system-version").Value;
                        string PKGConfigDRMType = PKGConfigurationXML.Descendants("config").First().Element("drm-type").Value;
                        string PKGConfigContentType = PKGConfigurationXML.Descendants("config").First().Element("content-type").Value;
                        string PKGConfigApplicationType = PKGConfigurationXML.Descendants("config").First().Element("application-type").Value;
                        string PKGConfigNumberOfImages = PKGConfigurationXML.Descendants("config").First().Element("num-of-images").Value;
                        string PKGConfigSize = PKGConfigurationXML.Descendants("config").First().Element("package-size").Value;
                        string PKGConfigVersionDate = PKGConfigurationXML.Descendants("config").First().Element("version-date").Value;
                        string PKGConfigVersionHash = PKGConfigurationXML.Descendants("config").First().Element("version-hash").Value;

                        // Get the PKG digests
                        var PKGDigests = PKGConfigurationXML.Element("digests");
                        if (PKGDigests is not null)
                        {
                            string PKGDigestsVersion = PKGDigests.Attribute("version")?.Value;
                            string PKGDigestsMajorParamVersion = PKGDigests.Attribute("major-param-version")?.Value;
                        }
                        string PKGContentDigest = PKGConfigurationXML.Descendants("digests").First().Element("content-digest").Value;
                        string PKGGameDigest = PKGConfigurationXML.Descendants("digests").First().Element("game-digest").Value;
                        string PKGHeaderDigest = PKGConfigurationXML.Descendants("digests").First().Element("header-digest").Value;
                        string PKGSystemDigest = PKGConfigurationXML.Descendants("digests").First().Element("system-digest").Value;
                        string PKGParamDigest = PKGConfigurationXML.Descendants("digests").First().Element("param-digest").Value;
                        string PKGDigest = PKGConfigurationXML.Descendants("digests").First().Element("package-digest").Value;

                        // Get the PKG params
                        string PKGParamApplicationDRMType = PKGConfigurationXML.Descendants("params").First().Element("applicationDrmType").Value;
                        string PKGParamContentID = PKGConfigurationXML.Descendants("params").First().Element("contentId").Value;
                        string PKGParamContentVersion = PKGConfigurationXML.Descendants("params").First().Element("contentVersion").Value;
                        string PKGParamMasterVersion = PKGConfigurationXML.Descendants("params").First().Element("masterVersion").Value;
                        string PKGParamRequiredSystemVersion = PKGConfigurationXML.Descendants("params").First().Element("requiredSystemSoftwareVersion").Value;
                        string PKGParamSDKVersion = PKGConfigurationXML.Descendants("params").First().Element("sdkVersion").Value;
                        string PKGParamTitleName = PKGConfigurationXML.Descendants("params").First().Element("titleName").Value;

                        // Get the PKG container information
                        string PKGContainerSize = PKGConfigurationXML.Descendants("container").First().Element("container-size").Value;
                        string PKGContainerMandatorySize = PKGConfigurationXML.Descendants("container").First().Element("mandatory-size").Value;
                        string PKGContainerBodyOffset = PKGConfigurationXML.Descendants("container").First().Element("body-offset").Value;
                        string PKGContainerBodySize = PKGConfigurationXML.Descendants("container").First().Element("body-size").Value;
                        string PKGContainerBodyDigest = PKGConfigurationXML.Descendants("container").First().Element("body-digest").Value;
                        string PKGContainerPromoteSize = PKGConfigurationXML.Descendants("container").First().Element("promote-size").Value;

                        // Get the PKG mount image
                        string PKGMountImagePFSOffsetAlign = PKGConfigurationXML.Descendants("mount-image").First().Element("pfs-offset-align").Value;
                        string PKGMountImagePFSSizeAlign = PKGConfigurationXML.Descendants("mount-image").First().Element("pfs-size-align").Value;
                        string PKGMountImagePFSImageOffset = PKGConfigurationXML.Descendants("mount-image").First().Element("pfs-image-offset").Value;
                        string PKGMountImagePFSImageSize = PKGConfigurationXML.Descendants("mount-image").First().Element("pfs-image-size").Value;
                        string PKGMountImageFixedInfoSize = PKGConfigurationXML.Descendants("mount-image").First().Element("fixed-info-size").Value;
                        string PKGMountImagePFSImageSeed = PKGConfigurationXML.Descendants("mount-image").First().Element("pfs-image-seed").Value;
                        string PKGMountImageSBlockDigest = PKGConfigurationXML.Descendants("mount-image").First().Element("sblock-digest").Value;
                        string PKGMountImageFixedInfoDigest = PKGConfigurationXML.Descendants("mount-image").First().Element("fixed-info-digest").Value;
                        string PKGMountImageOffset = PKGConfigurationXML.Descendants("mount-image").First().Element("mount-image-offset").Value;
                        string PKGMountImageSize = PKGConfigurationXML.Descendants("mount-image").First().Element("mount-image-size").Value;
                        string PKGMountImageContainerOffset = PKGConfigurationXML.Descendants("mount-image").First().Element("container-offset").Value;
                        string PKGMountImageSupplementalOffset = PKGConfigurationXML.Descendants("mount-image").First().Element("supplemental-offset").Value;

                        // Get the PKG entries and add to PKGContentListView
                        var PKGEntries = PKGConfigurationXML.Descendants("entries").Descendants("entry");
                        foreach (XElement PKGEntry in PKGEntries)
                        {
                            var NewPS5PKGEntry = new PS5PKGEntry() { EntryOffset = PKGEntry.Attribute("offset").Value, EntrySize = PKGEntry.Attribute("size").Value, EntryName = PKGEntry.Attribute("name").Value };
                            PKGContentListBox.Items.Add(NewPS5PKGEntry);
                        }

                        // Get the PKG chunkinfo
                        var PKGChunkInfo = PKGConfigurationXML.Element("chunkinfo");
                        if (PKGChunkInfo is not null)
                        {
                            string PKGChunkInfoSize = PKGChunkInfo.Attribute("size")?.Value;
                            string PKGChunkInfoNested = PKGChunkInfo.Attribute("nested")?.Value;
                            string PKGChunkInfoSDK = PKGChunkInfo.Attribute("sdk")?.Value;
                            string PKGChunkInfoDisps = PKGChunkInfo.Attribute("disps")?.Value;
                        }
                        string PKGChunkInfoContentID = PKGConfigurationXML.Descendants("chunkinfo").First().Element("contentid").Value;
                        string PKGChunkInfoLanguages = PKGConfigurationXML.Descendants("chunkinfo").First().Element("languages").Value;

                        // Get the PKG chunkinfo scenarios
                        var PKGChunkInfoScenarios = PKGConfigurationXML.Descendants("chunkinfo").Descendants("scenarios").Descendants("scenario");
                        foreach (XElement PKGChunkInfoScenario in PKGChunkInfoScenarios)
                        {
                            var NewPS5PKGChunkInfoScenario = new PS5PKGScenario()
                            {
                                ScenarioID = PKGChunkInfoScenario.Attribute("id").Value,
                                ScenarioType = PKGChunkInfoScenario.Attribute("type").Value,
                                ScenarioName = PKGChunkInfoScenario.Attribute("name").Value
                            };
                            PKGScenariosListBox.Items.Add(NewPS5PKGChunkInfoScenario);
                        }

                        // Get the PKG chunkinfo chunks
                        var PKGChunkInfoChunks = PKGConfigurationXML.Element("chunks");
                        if (PKGChunkInfoChunks is not null)
                        {
                            string PKGChunkInfoChunksNum = PKGChunkInfoChunks.Attribute("num")?.Value;
                            string PKGChunkInfoChunksDefault = PKGChunkInfoChunks.Attribute("default")?.Value;
                        }
                        var PKGChunkInfoChunksList = PKGConfigurationXML.Descendants("chunkinfo").Descendants("chunks").Descendants("chunk");
                        foreach (XElement PKGChunkInfoChunk in PKGChunkInfoChunksList)
                        {
                            var NewPS5PKGChunkInfoChunk = new PS5PKGChunk()
                            {
                                ChunkID = PKGChunkInfoChunk.Attribute("id").Value,
                                ChunkFlag = PKGChunkInfoChunk.Attribute("flag").Value,
                                ChunkLocus = PKGChunkInfoChunk.Attribute("locus").Value,
                                ChunkLanguage = PKGChunkInfoChunk.Attribute("language").Value,
                                ChunkDisps = PKGChunkInfoChunk.Attribute("disps").Value,
                                ChunkNum = PKGChunkInfoChunk.Attribute("num").Value,
                                ChunkSize = PKGChunkInfoChunk.Attribute("size").Value,
                                ChunkName = PKGChunkInfoChunk.Attribute("name").Value,
                                ChunkValue = PKGChunkInfoChunk.Value
                            };
                            PKGChunksListBox.Items.Add(NewPS5PKGChunkInfoChunk);
                        }

                        // Get the PKG chunkinfo outers
                        var PKGChunkInfoOuters = PKGConfigurationXML.Element("outers");
                        if (PKGChunkInfoOuters is not null)
                        {
                            string PKGChunkInfoOutersNum = PKGChunkInfoOuters.Attribute("num")?.Value;
                            string PKGChunkInfoOutersOverlapped = PKGChunkInfoOuters.Attribute("overlapped")?.Value;
                            string PKGChunkInfoOutersLanguageOverlapped = PKGChunkInfoOuters.Attribute("language-overlapped")?.Value;
                        }
                        var PKGChunkInfoOutersList = PKGConfigurationXML.Descendants("chunkinfo").Descendants("outers").Descendants("outer");
                        foreach (XElement PKGChunkInfoOuter in PKGChunkInfoOutersList)
                        {
                            var NewPS5PKGOuter = new PS5PKGOuter()
                            {
                                OuterID = PKGChunkInfoOuter.Attribute("id").Value,
                                OuterImage = PKGChunkInfoOuter.Attribute("image").Value,
                                OuterOffset = PKGChunkInfoOuter.Attribute("offset").Value,
                                OuterSize = PKGChunkInfoOuter.Attribute("size").Value,
                                OuterChunks = PKGChunkInfoOuter.Attribute("chunks").Value
                            };
                            PKGOutersListBox.Items.Add(NewPS5PKGOuter);
                        }

                        // Get the PKG pfs image info
                        var PKGPFSImage = PKGConfigurationXML.Element("pfs-image");
                        if (PKGPFSImage is not null)
                        {
                            string PKGPFSImageVersion = PKGPFSImage.Attribute("version")?.Value;
                            string PKGPFSImageReadOnly = PKGPFSImage.Attribute("readonly")?.Value;
                            string PKGPFSImageOffset = PKGPFSImage.Attribute("offset")?.Value;
                            string PKGPFSImageMetadata = PKGPFSImage.Attribute("metadata")?.Value;
                        }

                        // Get the PKG pfs image sblock info
                        var PKGPFSImageSBlock = PKGConfigurationXML.Descendants("sblock").FirstOrDefault();
                        if (PKGPFSImageSBlock is not null)
                        {
                            string PKGPFSImageSBlockSigned = PKGPFSImageSBlock.Attribute("signed")?.Value;
                            string PKGPFSImageSBlockEncrypted = PKGPFSImageSBlock.Attribute("encrypted")?.Value;
                            string PKGPFSImageSBlockIgnoreCase = PKGPFSImageSBlock.Attribute("ignore-case")?.Value;
                            string PKGPFSImageSBlockIndexSize = PKGPFSImageSBlock.Attribute("index-size")?.Value;
                            string PKGPFSImageSBlockBlocks = PKGPFSImageSBlock.Attribute("blocks")?.Value;
                            string PKGPFSImageSBlockBackups = PKGPFSImageSBlock.Attribute("backups")?.Value;
                        }
                        var PKGPFSImageSBlockImageSize = PKGConfigurationXML.Descendants("sblock").FirstOrDefault().Element("image-size");
                        if (PKGPFSImageSBlockImageSize is not null)
                        {
                            string PKGPFSImageSBlockImageSizeBlockSize = PKGPFSImageSBlockImageSize.Attribute("block-size")?.Value;
                            string PKGPFSImageSBlockImageSizeNum = PKGPFSImageSBlockImageSize.Attribute("num")?.Value;
                            string PKGPFSImageSBlockImageSizeValue = PKGPFSImageSBlockImageSize.Value;
                        }
                        var PKGPFSImageSBlockSuperInode = PKGConfigurationXML.Descendants("sblock").FirstOrDefault().Element("super-inode");
                        if (PKGPFSImageSBlockSuperInode is not null)
                        {
                            string PKGPFSImageSBlockSuperInodeBlocks = PKGPFSImageSBlockSuperInode.Attribute("blocks")?.Value;
                            string PKGPFSImageSBlockSuperInodeInodes = PKGPFSImageSBlockSuperInode.Attribute("inodes")?.Value;
                            string PKGPFSImageSBlockSuperInodeRoot = PKGPFSImageSBlockSuperInode.Attribute("root")?.Value;
                        }
                        var PKGPFSImageSBlockInode = PKGConfigurationXML.Descendants("sblock").FirstOrDefault().Descendants("super-inode").FirstOrDefault().Element("inode");
                        if (PKGPFSImageSBlockInode is not null)
                        {
                            string PKGPFSImageSBlockInodeSize = PKGPFSImageSBlockInode.Attribute("size")?.Value;
                            string PKGPFSImageSBlockInodeLinks = PKGPFSImageSBlockInode.Attribute("links")?.Value;
                            string PKGPFSImageSBlockInodeMode = PKGPFSImageSBlockInode.Attribute("mode")?.Value;
                            string PKGPFSImageSBlockInodeIMode = PKGPFSImageSBlockInode.Attribute("imode")?.Value;
                            string PKGPFSImageSBlockInodeIndex = PKGPFSImageSBlockInode.Attribute("index")?.Value;
                        }
                        string PKGPFSImageSBlockSeed = PKGConfigurationXML.Descendants("sblock").FirstOrDefault().Element("seed").Value;
                        string PKGPFSImageSBlockICV = PKGConfigurationXML.Descendants("sblock").FirstOrDefault().Element("icv").Value;

                        // Get the PKG pfs image root info
                        var PKGPFSImageRoot = PKGConfigurationXML.Descendants("pfs-image").FirstOrDefault().Element("root");
                        if (PKGPFSImageRoot is not null)
                        {
                            string PKGPFSImageRootSize = PKGPFSImageRoot.Attribute("size")?.Value;
                            string PKGPFSImageRootLinks = PKGPFSImageRoot.Attribute("links")?.Value;
                            string PKGPFSImageRootIMode = PKGPFSImageRoot.Attribute("imode")?.Value;
                            string PKGPFSImageRootIndex = PKGPFSImageRoot.Attribute("index")?.Value;
                            string PKGPFSImageRootINode = PKGPFSImageRoot.Attribute("inode")?.Value;
                            string PKGPFSImageRootName = PKGPFSImageRoot.Attribute("name")?.Value;
                        }
                        // Get the files in root
                        var PKGPFSImageRootFiles = PKGConfigurationXML.Descendants("pfs-image").FirstOrDefault().Descendants("root").FirstOrDefault().Descendants("file");
                        foreach (XElement PKGPFSImageRootFile in PKGPFSImageRootFiles)
                        {
                            PS5PKGRootFile @init = new PS5PKGRootFile();
                            var NewPS5PKGRootFile = (@init.FileSize = PKGPFSImageRootFile.Attribute("size")?.Value, @init.FilePlain = PKGPFSImageRootFile.Attribute("plain")?.Value, @init.FileCompression = PKGPFSImageRootFile.Attribute("comp")?.Value, @init.FileIMode = PKGPFSImageRootFile.Attribute("imode")?.Value, @init.FileIndex = PKGPFSImageRootFile.Attribute("index")?.Value, @init.FileINode = PKGPFSImageRootFile.Attribute("inode")?.Value, @init.FileName = PKGPFSImageRootFile.Attribute("name")?.Value, @init).@init;
                            PFSImageRootFiles.Add(NewPS5PKGRootFile);
                        }
                        // Get the directories in root
                        var PKGPFSImageRootDirectories = PKGConfigurationXML.Descendants("pfs-image").FirstOrDefault().Descendants("root").FirstOrDefault().Descendants("dir");
                        foreach (XElement PKGPFSImageRootDirectory in PKGPFSImageRootDirectories)
                        {
                            PS5PKGRootDirectory @init1 = new PS5PKGRootDirectory();
                            var NewPS5PKGRootDirectory = (@init1.DirectorySize = PKGPFSImageRootDirectory.Attribute("size")?.Value, @init1.DirectoryLinks = PKGPFSImageRootDirectory.Attribute("links")?.Value, @init1.DirectoryIMode = PKGPFSImageRootDirectory.Attribute("imode")?.Value, @init1.DirectoryIndex = PKGPFSImageRootDirectory.Attribute("index")?.Value, @init1.DirectoryINode = PKGPFSImageRootDirectory.Attribute("inode")?.Value, @init1.DirectoryName = PKGPFSImageRootDirectory.Attribute("name")?.Value, @init1).@init1;
                            PFSImageRootDirectories.Add(NewPS5PKGRootDirectory);
                        }
                        // Get the files in uroot
                        var PKGPFSImageURootFiles = PKGConfigurationXML.Descendants("pfs-image").FirstOrDefault().Descendants("root").FirstOrDefault().Descendants("dir").FirstOrDefault().Descendants("file");
                        foreach (XElement PKGPFSImageURootFile in PKGPFSImageURootFiles)
                        {
                            PS5PKGRootFile @init2 = new PS5PKGRootFile();
                            var NewPS5PKGURootFile = (@init2.FileSize = PKGPFSImageURootFile.Attribute("size")?.Value, @init2.FilePlain = PKGPFSImageURootFile.Attribute("plain")?.Value, @init2.FileCompression = PKGPFSImageURootFile.Attribute("comp")?.Value, @init2.FileIMode = PKGPFSImageURootFile.Attribute("imode")?.Value, @init2.FileIndex = PKGPFSImageURootFile.Attribute("index")?.Value, @init2.FileINode = PKGPFSImageURootFile.Attribute("inode")?.Value, @init2.FileName = PKGPFSImageURootFile.Attribute("name")?.Value, @init2).@init2;
                            PFSImageURootFiles.Add(NewPS5PKGURootFile);
                        }

                        // Get the PKG nested image info
                        var PKGNestedImage = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault();
                        if (PKGNestedImage is not null)
                        {
                            string PKGNestedImageVersion = PKGNestedImage.Attribute("version")?.Value;
                            string PKGNestedImageReadOnly = PKGNestedImage.Attribute("readonly")?.Value;
                            string PKGNestedImageOffset = PKGNestedImage.Attribute("offset")?.Value;
                        }
                        // Get the PKG nested image sblock info
                        var PKGNestedImageSBlock = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault().Descendants("sblock").FirstOrDefault();
                        if (PKGNestedImageSBlock is not null)
                        {
                            string PKGPFSImageSBlockSigned = PKGNestedImageSBlock.Attribute("signed")?.Value;
                            string PKGPFSImageSBlockEncrypted = PKGNestedImageSBlock.Attribute("encrypted")?.Value;
                            string PKGPFSImageSBlockIgnoreCase = PKGNestedImageSBlock.Attribute("ignore-case")?.Value;
                            string PKGPFSImageSBlockIndexSize = PKGNestedImageSBlock.Attribute("index-size")?.Value;
                            string PKGPFSImageSBlockBlocks = PKGNestedImageSBlock.Attribute("blocks")?.Value;
                            string PKGPFSImageSBlockBackups = PKGNestedImageSBlock.Attribute("backups")?.Value;
                        }
                        var PKGNestedImageSBlockImageSize = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault().Descendants("sblock").FirstOrDefault().Element("image-size");
                        if (PKGNestedImageSBlockImageSize is not null)
                        {
                            string PKGPFSImageSBlockImageSizeBlockSize = PKGNestedImageSBlockImageSize.Attribute("block-size")?.Value;
                            string PKGPFSImageSBlockImageSizeNum = PKGNestedImageSBlockImageSize.Attribute("num")?.Value;
                            string PKGPFSImageSBlockImageSizeValue = PKGNestedImageSBlockImageSize.Value;
                        }
                        var PKGNestedImageSBlockSuperInode = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault().Descendants("sblock").FirstOrDefault().Element("super-inode");
                        if (PKGNestedImageSBlockSuperInode is not null)
                        {
                            string PKGPFSImageSBlockSuperInodeBlocks = PKGNestedImageSBlockSuperInode.Attribute("blocks")?.Value;
                            string PKGPFSImageSBlockSuperInodeInodes = PKGNestedImageSBlockSuperInode.Attribute("inodes")?.Value;
                            string PKGPFSImageSBlockSuperInodeRoot = PKGNestedImageSBlockSuperInode.Attribute("root")?.Value;
                        }
                        var PKGNestedImageSBlockInode = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault().Descendants("sblock").FirstOrDefault().Descendants("super-inode").FirstOrDefault().Element("inode");
                        if (PKGNestedImageSBlockInode is not null)
                        {
                            string PKGPFSImageSBlockInodeSize = PKGNestedImageSBlockInode.Attribute("size")?.Value;
                            string PKGPFSImageSBlockInodeLinks = PKGNestedImageSBlockInode.Attribute("links")?.Value;
                            string PKGPFSImageSBlockInodeMode = PKGNestedImageSBlockInode.Attribute("mode")?.Value;
                            string PKGPFSImageSBlockInodeIMode = PKGNestedImageSBlockInode.Attribute("imode")?.Value;
                            string PKGPFSImageSBlockInodeIndex = PKGNestedImageSBlockInode.Attribute("index")?.Value;
                        }
                        // Get the PKG nested image metadata
                        var PKGNestedImageMetadata = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault().Descendants("metadata").FirstOrDefault();
                        if (PKGNestedImageMetadata is not null)
                        {
                            string PKGNestedImageMetadataSize = PKGNestedImageMetadata.Attribute("size")?.Value;
                            string PKGNestedImageMetadataPlain = PKGNestedImageMetadata.Attribute("plain")?.Value;
                            string PKGNestedImageMetadataCompression = PKGNestedImageMetadata.Attribute("comp")?.Value;
                            string PKGNestedImageMetadataOffset = PKGNestedImageMetadata.Attribute("offset")?.Value;
                            string PKGNestedImageMetadataPOffset = PKGNestedImageMetadata.Attribute("poffset")?.Value;
                            string PKGNestedImageMetadataAfid = PKGNestedImageMetadata.Attribute("afid")?.Value;
                        }

                        // Get the PKG nested image root info
                        var PKGNestedImageRoot = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault().Element("root");
                        if (PKGNestedImageRoot is not null)
                        {
                            string PKGPFSImageRootSize = PKGNestedImageRoot.Attribute("size")?.Value;
                            string PKGPFSImageRootLinks = PKGNestedImageRoot.Attribute("links")?.Value;
                            string PKGPFSImageRootIMode = PKGNestedImageRoot.Attribute("imode")?.Value;
                            string PKGPFSImageRootIndex = PKGNestedImageRoot.Attribute("index")?.Value;
                            string PKGPFSImageRootINode = PKGNestedImageRoot.Attribute("inode")?.Value;
                            string PKGPFSImageRootName = PKGNestedImageRoot.Attribute("name")?.Value;
                        }
                        // Get the files in root
                        var PKGNestedImageRootFiles = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault().Descendants("root").FirstOrDefault().Descendants("file");
                        foreach (XElement PKGNestedImageRootFile in PKGNestedImageRootFiles)
                        {
                            PS5PKGRootFile @init3 = new PS5PKGRootFile();
                            var NewPS5PKGRootFile = (@init3.FileSize = PKGNestedImageRootFile.Attribute("size")?.Value, @init3.FilePlain = PKGNestedImageRootFile.Attribute("plain")?.Value, @init3.FileCompression = PKGNestedImageRootFile.Attribute("comp")?.Value, @init3.FileIMode = PKGNestedImageRootFile.Attribute("imode")?.Value, @init3.FileIndex = PKGNestedImageRootFile.Attribute("index")?.Value, @init3.FileINode = PKGNestedImageRootFile.Attribute("inode")?.Value, @init3.FileName = PKGNestedImageRootFile.Attribute("name")?.Value, @init3).@init3;
                            NestedImageRootFiles.Add(NewPS5PKGRootFile);
                        }
                        // Get the directories in root
                        var PKGNestedImageRootDirectories = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault().Descendants("root").FirstOrDefault().Descendants("dir");
                        foreach (XElement PKGNestedImageRootDirectory in PKGNestedImageRootDirectories)
                        {
                            PS5PKGRootDirectory @init4 = new PS5PKGRootDirectory();
                            var NewPS5PKGRootDirectory = (@init4.DirectorySize = PKGNestedImageRootDirectory.Attribute("size")?.Value, @init4.DirectoryLinks = PKGNestedImageRootDirectory.Attribute("links")?.Value, @init4.DirectoryIMode = PKGNestedImageRootDirectory.Attribute("imode")?.Value, @init4.DirectoryIndex = PKGNestedImageRootDirectory.Attribute("index")?.Value, @init4.DirectoryINode = PKGNestedImageRootDirectory.Attribute("inode")?.Value, @init4.DirectoryName = PKGNestedImageRootDirectory.Attribute("name")?.Value, @init4).@init4;
                            NestedImageRootDirectories.Add(NewPS5PKGRootDirectory);
                        }
                        // Get the files in uroot
                        var PKGNestedImageURootFiles = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault().Descendants("root").FirstOrDefault().Descendants("dir").FirstOrDefault().Descendants("file");
                        foreach (XElement PKGNestedImageURootFile in PKGNestedImageURootFiles)
                        {
                            PS5PKGRootFile @init5 = new PS5PKGRootFile();
                            var NewPS5PKGURootFile = (@init5.FileSize = PKGNestedImageURootFile.Attribute("size")?.Value, @init5.FilePlain = PKGNestedImageURootFile.Attribute("plain")?.Value, @init5.FileCompression = PKGNestedImageURootFile.Attribute("comp")?.Value, @init5.FileIMode = PKGNestedImageURootFile.Attribute("imode")?.Value, @init5.FileIndex = PKGNestedImageURootFile.Attribute("index")?.Value, @init5.FileINode = PKGNestedImageURootFile.Attribute("inode")?.Value, @init5.FileName = PKGNestedImageURootFile.Attribute("name")?.Value, @init5).@init5;
                            NestedImageURootFiles.Add(NewPS5PKGURootFile);
                        }
                        // Get the directories in uroot
                        var PKGNestedImageURootDirectories = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault().Descendants("root").FirstOrDefault().Descendants("dir").FirstOrDefault().Descendants("dir");
                        foreach (XElement PKGNestedImageURootDirectory in PKGNestedImageURootDirectories)
                        {
                            PS5PKGRootDirectory @init6 = new PS5PKGRootDirectory();
                            var NewPS5PKGRootDirectory = (@init6.DirectorySize = PKGNestedImageURootDirectory.Attribute("size")?.Value, @init6.DirectoryLinks = PKGNestedImageURootDirectory.Attribute("links")?.Value, @init6.DirectoryIMode = PKGNestedImageURootDirectory.Attribute("imode")?.Value, @init6.DirectoryIndex = PKGNestedImageURootDirectory.Attribute("index")?.Value, @init6.DirectoryINode = PKGNestedImageURootDirectory.Attribute("inode")?.Value, @init6.DirectoryName = PKGNestedImageURootDirectory.Attribute("name")?.Value, @init6).@init6;
                            NestedImageRootDirectories.Add(NewPS5PKGRootDirectory);
                        }

                        // Extract param.json & icon0.png
                        using (var PKGReader = new FileStream(selectedFile[0], FileMode.Open, FileAccess.Read))
                        {
                            // Seek from the end
                            PKGReader.Seek(0L, SeekOrigin.End);

                            long ContainerOffsetDecValue = 0L;
                            long EntryOffsetDecValue = 0L;
                            int EntrySizeDecValue = 0;

                            long ParamJSONOffsetPosition = 0L;
                            long Icon0OffsetPosition = 0L;
                            long Pic0OffsetPosition = 0L;

                            long PKGFileLength = PKGReader.Length;

                            var ParamJsonPKGEntry = new PS5PKGEntry();
                            var Icon0PKGEntry = new PS5PKGEntry();
                            var Pic0PKGEntry = new PS5PKGEntry();

                            // Get the param.json and icon0.png PKG entry info
                            foreach (XElement PKGEntry in PKGConfigurationXML.Descendants("entries").Descendants("entry"))
                            {
                                if (PKGEntry is not null)
                                {
                                    if (PKGEntry.Attribute("name").Value == "param.json")
                                    {
                                        ParamJsonPKGEntry.EntryOffset = PKGEntry.Attribute("offset").Value;
                                        ParamJsonPKGEntry.EntrySize = PKGEntry.Attribute("size").Value;
                                        ParamJsonPKGEntry.EntryName = PKGEntry.Attribute("name").Value;
                                    }
                                    if (PKGEntry.Attribute("name").Value == "icon0.png")
                                    {
                                        Icon0PKGEntry.EntryOffset = PKGEntry.Attribute("offset").Value;
                                        Icon0PKGEntry.EntrySize = PKGEntry.Attribute("size").Value;
                                        Icon0PKGEntry.EntryName = PKGEntry.Attribute("name").Value;
                                    }
                                    if (PKGEntry.Attribute("name").Value == "pic0.png")
                                    {
                                        Pic0PKGEntry.EntryOffset = PKGEntry.Attribute("offset").Value;
                                        Pic0PKGEntry.EntrySize = PKGEntry.Attribute("size").Value;
                                        Pic0PKGEntry.EntryName = PKGEntry.Attribute("name").Value;
                                    }
                                }
                            }

                            // PARAM.JSON
                            if (!string.IsNullOrEmpty(ParamJsonPKGEntry.EntryOffset) && !string.IsNullOrEmpty(ParamJsonPKGEntry.EntrySize))
                            {

                                // Get decimal offset values
                                if (!string.IsNullOrEmpty(PKGMountImageContainerOffset))
                                {
                                    ContainerOffsetDecValue = Convert.ToInt64(PKGMountImageContainerOffset, 16);
                                }
                                if (!string.IsNullOrEmpty(ParamJsonPKGEntry.EntryOffset))
                                {
                                    EntryOffsetDecValue = Convert.ToInt64(ParamJsonPKGEntry.EntryOffset, 16);
                                }
                                if (!string.IsNullOrEmpty(ParamJsonPKGEntry.EntrySize))
                                {
                                    EntrySizeDecValue = Convert.ToInt32(ParamJsonPKGEntry.EntrySize, 16);
                                }
                                ParamJSONOffsetPosition = ContainerOffsetDecValue + EntryOffsetDecValue;

                                // Seek to the beginning of the param.json file and read
                                byte[] ParamFileBuffer = new byte[EntrySizeDecValue];
                                PKGReader.Seek(ParamJSONOffsetPosition, SeekOrigin.Begin);
                                PKGReader.Read(ParamFileBuffer, 0, ParamFileBuffer.Length);

                                if (!string.IsNullOrWhiteSpace(Encoding.UTF8.GetString(ParamFileBuffer)))
                                {
                                    CurrentParamJSON = Encoding.UTF8.GetString(ParamFileBuffer);
                                    var ParamData = JsonConvert.DeserializeObject<PS5ParamClass.PS5Param>(Encoding.UTF8.GetString(ParamFileBuffer));
                                    var NewPS5Game = new PS5Game() { GameBackupType = "PKG" };

                                    if (ParamData is not null)
                                    {
                                        if (ParamData.TitleId is not null)
                                        {
                                            NewPS5Game.GameID = "Title ID: " + ParamData.TitleId;
                                            NewPS5Game.GameRegion = "Region: " + PS5Game.GetGameRegion(ParamData.TitleId);
                                        }

                                        if (ParamData.LocalizedParameters.EnUS is not null)
                                        {
                                            NewPS5Game.GameTitle = ParamData.LocalizedParameters.EnUS.TitleName;
                                        }
                                        if (ParamData.LocalizedParameters.DeDE is not null)
                                        {
                                            NewPS5Game.DEGameTitle = ParamData.LocalizedParameters.DeDE.TitleName;
                                        }
                                        if (ParamData.LocalizedParameters.FrFR is not null)
                                        {
                                            NewPS5Game.FRGameTitle = ParamData.LocalizedParameters.FrFR.TitleName;
                                        }
                                        if (ParamData.LocalizedParameters.ItIT is not null)
                                        {
                                            NewPS5Game.ITGameTitle = ParamData.LocalizedParameters.ItIT.TitleName;
                                        }
                                        if (ParamData.LocalizedParameters.EsES is not null)
                                        {
                                            NewPS5Game.ESGameTitle = ParamData.LocalizedParameters.EsES.TitleName;
                                        }
                                        if (ParamData.LocalizedParameters.JaJP is not null)
                                        {
                                            NewPS5Game.JPGameTitle = ParamData.LocalizedParameters.JaJP.TitleName;
                                        }

                                        if (ParamData.ContentId is not null)
                                        {
                                            NewPS5Game.GameContentID = "Content ID: " + ParamData.ContentId;
                                        }

                                        if (ParamData.ApplicationCategoryType == 0)
                                        {
                                            NewPS5Game.GameCategory = "Type: PS5 Game";
                                        }
                                        else if (ParamData.ApplicationCategoryType == 65792)
                                        {
                                            NewPS5Game.GameCategory = "Type: RNPS Media App";
                                        }
                                        else if (ParamData.ApplicationCategoryType == 131328)
                                        {
                                            NewPS5Game.GameCategory = "Type: System Built-in App";
                                        }
                                        else if (ParamData.ApplicationCategoryType == 131584)
                                        {
                                            NewPS5Game.GameCategory = "Type: Big Daemon";
                                        }
                                        else if (ParamData.ApplicationCategoryType == 16777216)
                                        {
                                            NewPS5Game.GameCategory = "Type: ShellUI";
                                        }
                                        else if (ParamData.ApplicationCategoryType == 33554432)
                                        {
                                            NewPS5Game.GameCategory = "Type: Daemon";
                                        }
                                        else if (ParamData.ApplicationCategoryType == 67108864)
                                        {
                                            NewPS5Game.GameCategory = "Type: ShellApp";
                                        }

                                        NewPS5Game.GameSize = "Size: " + Strings.FormatNumber(PKGFileLength / 1073741824d, 2) + " GB";

                                        if (ParamData.ContentVersion is not null)
                                        {
                                            NewPS5Game.GameVersion = "Version: " + ParamData.ContentVersion;
                                        }
                                        if (ParamData.RequiredSystemSoftwareVersion is not null)
                                        {
                                            NewPS5Game.GameRequiredFirmware = "Required Firmware: " + ParamData.RequiredSystemSoftwareVersion.Replace("0x", "").Insert(2, ".").Insert(5, ".").Insert(8, ".").Remove(11, 8);
                                        }

                                        GameTitleTextBlock.IsVisible = true;
                                        GameIDTextBlock.IsVisible = true;
                                        GameRegionTextBlock.IsVisible = true;
                                        GameVersionTextBlock.IsVisible = true;
                                        GameContentIDTextBlock.IsVisible = true;
                                        GameCategoryTextBlock.IsVisible = true;
                                        GameSizeTextBlock.IsVisible = true;
                                        GameRequiredFirmwareTextBlock.IsVisible = true;

                                        GameTitleTextBlock.Text = NewPS5Game.GameTitle;
                                        GameIDTextBlock.Text = NewPS5Game.GameID;
                                        GameRegionTextBlock.Text = NewPS5Game.GameRegion;
                                        GameVersionTextBlock.Text = NewPS5Game.GameVersion;
                                        GameContentIDTextBlock.Text = NewPS5Game.GameContentID;
                                        GameCategoryTextBlock.Text = NewPS5Game.GameCategory;
                                        GameSizeTextBlock.Text = NewPS5Game.GameSize;
                                        GameRequiredFirmwareTextBlock.Text = NewPS5Game.GameRequiredFirmware;
                                    }
                                }

                            }

                            // ICON0.PNG
                            if (!string.IsNullOrEmpty(Icon0PKGEntry.EntryOffset) && !string.IsNullOrEmpty(Icon0PKGEntry.EntrySize))
                            {

                                // Get decimal offset values
                                if (!string.IsNullOrEmpty(PKGMountImageContainerOffset))
                                {
                                    ContainerOffsetDecValue = Convert.ToInt64(PKGMountImageContainerOffset, 16);
                                }
                                if (!string.IsNullOrEmpty(Icon0PKGEntry.EntryOffset))
                                {
                                    EntryOffsetDecValue = Convert.ToInt64(Icon0PKGEntry.EntryOffset, 16);
                                }
                                if (!string.IsNullOrEmpty(Icon0PKGEntry.EntrySize))
                                {
                                    EntrySizeDecValue = Convert.ToInt32(Icon0PKGEntry.EntrySize, 16);
                                }
                                Icon0OffsetPosition = ContainerOffsetDecValue + EntryOffsetDecValue;

                                // Seek to the beginning of the icon0.png file and read
                                byte[] Icon0FileBuffer = new byte[EntrySizeDecValue];
                                PKGReader.Seek(Icon0OffsetPosition, SeekOrigin.Begin);
                                PKGReader.Read(Icon0FileBuffer, 0, Icon0FileBuffer.Length);

                                // Check the buffer and display the icon
                                if (Icon0FileBuffer is not null)
                                {
                                    Bitmap Icon0BitmapImage;
                                    using (var Icon0MemoryStream = new MemoryStream(Icon0FileBuffer))
                                    {
                                        Icon0BitmapImage = new Bitmap(Icon0MemoryStream);
                                    }
                                    PKGIconImage.Source = Icon0BitmapImage;
                                    CurrentIcon0 = Icon0BitmapImage;
                                }
                            }

                            // PIC0.PNG
                            if (!string.IsNullOrEmpty(Pic0PKGEntry.EntryOffset) && !string.IsNullOrEmpty(Pic0PKGEntry.EntrySize))
                            {

                                // Get decimal offset values
                                if (!string.IsNullOrEmpty(PKGMountImageContainerOffset))
                                {
                                    ContainerOffsetDecValue = Convert.ToInt64(PKGMountImageContainerOffset, 16);
                                }
                                if (!string.IsNullOrEmpty(Pic0PKGEntry.EntryOffset))
                                {
                                    EntryOffsetDecValue = Convert.ToInt64(Pic0PKGEntry.EntryOffset, 16);
                                }
                                if (!string.IsNullOrEmpty(Pic0PKGEntry.EntrySize))
                                {
                                    EntrySizeDecValue = Convert.ToInt32(Pic0PKGEntry.EntrySize, 16);
                                }
                                Pic0OffsetPosition = ContainerOffsetDecValue + EntryOffsetDecValue;

                                // Seek to the beginning of the icon0.png file and read
                                byte[] Pic0FileBuffer = new byte[EntrySizeDecValue];
                                PKGReader.Seek(Pic0OffsetPosition, SeekOrigin.Begin);
                                PKGReader.Read(Pic0FileBuffer, 0, Pic0FileBuffer.Length);

                                // Check the buffer and display the icon
                                if (Pic0FileBuffer is not null)
                                {
                                    Bitmap Pic0BitmapImage;
                                    using (var Pic0MemoryStream = new MemoryStream(Pic0FileBuffer))
                                    {
                                        Pic0BitmapImage = new Bitmap(Pic0MemoryStream);
                                    }
                                    CurrentPic0 = Pic0BitmapImage;
                                }
                            }

                            PKGReader.Close();
                        }
                    }               
                }
            }
        }

        private void ShowPKGPFSImageFilesButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentListViewTitleTextBlock.Text = "PKG PFS Image Files :";

            HideListBoxes();
            PKGImageFilesListBox.IsVisible = true;
            PKGImageFilesListBox.Items.Clear();

            if (PFSImageRootFiles is not null && PFSImageRootFiles.Count > 0)
            {
                foreach (var PFSImageRootFile in PFSImageRootFiles)
                    PKGImageFilesListBox.Items.Add(PFSImageRootFile);
            }
        }

        private void ShowPKGPFSImageDirectoriesButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentListViewTitleTextBlock.Text = "PKG PFS Image Directories :";

            HideListBoxes();
            PKGImageDirectoriesListBox.IsVisible = true;
            PKGImageDirectoriesListBox.Items.Clear();

            if (PFSImageRootDirectories is not null && PFSImageRootDirectories.Count > 0)
            {
                foreach (var PFSImageRootDirectory in PFSImageRootDirectories)
                    PKGImageDirectoriesListBox.Items.Add(PFSImageRootDirectory);
            }
        }

        private void ShowPKGNestedImageFilesButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentListViewTitleTextBlock.Text = "PKG Nested Image Files :";

            HideListBoxes();
            PKGImageFilesListBox.IsVisible = true;
            PKGImageFilesListBox.Items.Clear();

            if (NestedImageRootFiles is not null && NestedImageRootFiles.Count > 0)
            {
                foreach (var NestedImageRootFile in NestedImageRootFiles)
                    PKGImageFilesListBox.Items.Add(NestedImageRootFile);
            }
        }

        private void ShowPKGNestedImageDirectoriesButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentListViewTitleTextBlock.Text = "PKG Nested Image Directories :";

            HideListBoxes();
            PKGImageDirectoriesListBox.IsVisible = true;
            PKGImageDirectoriesListBox.Items.Clear();

            if (NestedImageRootDirectories is not null && NestedImageRootDirectories.Count > 0)
            {
                foreach (var NestedImageRootDirectory in NestedImageRootDirectories)
                    PKGImageDirectoriesListBox.Items.Add(NestedImageRootDirectory);
            }
        }

        private void ShowPKGScenariosButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentListViewTitleTextBlock.Text = "PKG Scenarios :";

            HideListBoxes();
            PKGScenariosListBox.IsVisible = true;
        }

        private void ShowPKGChunksButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentListViewTitleTextBlock.Text = "PKG Chunks :";

            HideListBoxes();
            PKGChunksListBox.IsVisible = true;
        }

        private void ShowPKGOutersButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentListViewTitleTextBlock.Text = "PKG Outers :";

            HideListBoxes();
            PKGOutersListBox.IsVisible = true;
        }

        private void ShowPKGEntriesButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentListViewTitleTextBlock.Text = "PKG Entries :";

            HideListBoxes();
            PKGContentListBox.IsVisible = true;
        }

        private async void ExportConfigurationXMLButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentConfigurationXML is not null)
            {
                if (VisualRoot is not Window window)
                    return;

                var newSaveFileDialog = new SaveFileDialog() { Title = "Select a save path", Filters = [new FileDialogFilter() { Name = "XML files", Extensions = { "xml" } }] };
                var selectedSaveFile = await newSaveFileDialog.ShowAsync(window);
                if (selectedSaveFile != null)
                {
                    CurrentConfigurationXML.Save(selectedSaveFile);
                }
            }
        }

        private async void ExportParamJSONButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CurrentParamJSON))
            {
                if (VisualRoot is not Window window)
                    return;

                var newSaveFileDialog = new SaveFileDialog() { Title = "Select a save path", Filters = [new FileDialogFilter() { Name = "JSON files", Extensions = { "json" } }] };
                var selectedSaveFile = await newSaveFileDialog.ShowAsync(window);
                if (selectedSaveFile != null)
                {
                    File.WriteAllText(selectedSaveFile, CurrentParamJSON);
                }
            }
        }

        private async void ExportIcon0PNGButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentIcon0 is not null)
            {
                if (VisualRoot is not Window window)
                    return;

                var newSaveFileDialog = new SaveFileDialog() { Title = "Select a save path", Filters = [new FileDialogFilter() { Name = "PNG files", Extensions = { "png" } }] };
                var selectedSaveFile = await newSaveFileDialog.ShowAsync(window);
                if (selectedSaveFile != null)
                {
                    CurrentIcon0.Save(selectedSaveFile);
                }
            }
        }

        private async void ExportPic0Button_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPic0 is not null)
            {
                if (VisualRoot is not Window window)
                    return;

                var newSaveFileDialog = new SaveFileDialog() { Title = "Select a save path", Filters = [new FileDialogFilter() { Name = "PNG files", Extensions = { "png" } }] };
                var selectedSaveFile = await newSaveFileDialog.ShowAsync(window);
                if (selectedSaveFile != null)
                {
                    CurrentPic0.Save(selectedSaveFile);
                }
            }
        }

        private void HideListBoxes()
        {
            PKGContentListBox.IsVisible = false;
            PKGScenariosListBox.IsVisible = false;
            PKGChunksListBox.IsVisible = false;
            PKGOutersListBox.IsVisible = false;
            PKGImageFilesListBox.IsVisible = false;
            PKGImageDirectoriesListBox.IsVisible = false;
        }

        private bool MatchBytes(byte[] buffer, int position, byte[] pattern)
        {
            if (position + 1 < pattern.Length)
            {
                return false;
            }

            for (int i = 0, loopTo = pattern.Length - 1; i <= loopTo; i++)
            {
                if (buffer[position - i] != pattern[pattern.Length - 1 - i])
                {
                    return false;
                }
            }

            return true;
        }

        #region Structures & Classes

        public class PS5ParamClass
        {
            public class AgeLevel
            {
                private int _US;
                private int _Default;
                private int _AE;
                private int _AR;
                private int _AT;
                private int _AU;
                private int _BE;
                private int _BG;
                private int _BH;
                private int _BO;
                private int _BR;
                private int _CA;
                private int _CH;
                private int _CL;
                private int _CN;
                private int _CO;
                private int _CR;
                private int _CY;
                private int _CZ;
                private int _DE;
                private int _DK;
                private int _EC;
                private int _ES;
                private int _FI;
                private int _FR;
                private int _GB;
                private int _GR;
                private int _GT;
                private int _HK;
                private int _HN;
                private int _HR;
                private int _HU;
                private int _ID;
                private int _IE;
                private int _IL;
                private int _IT;
                private int _JP;
                private int _KR;
                private int _KW;
                private int _LB;
                private int _LU;
                private int _MT;
                private int _MX;
                private int _MY;
                private int _NI;
                private int _NL;
                private int _NO;
                private int _NZ;
                private int _OM;
                private int _PA;
                private int _PE;
                private int _PL;
                private int _PT;
                private int _PY;
                private int _QA;
                private int _RO;
                private int _RU;
                private int _SA;
                private int _SE;
                private int _SG;
                private int _SI;
                private int _SK;
                private int _SV;
                private int _TH;
                private int _TR;
                private int _TW;
                private int _UA;
                private int _UY;
                private int _ZA;
                private int _India;
                private int _Iceland;

                [JsonProperty("US")]
                public int US
                {
                    get
                    {
                        return _US;
                    }
                    set
                    {
                        _US = value;
                    }
                }

                [JsonProperty("AE")]
                public int AE
                {
                    get
                    {
                        return _AE;
                    }
                    set
                    {
                        _AE = value;
                    }
                }

                [JsonProperty("AR")]
                public int AR
                {
                    get
                    {
                        return _AR;
                    }
                    set
                    {
                        _AR = value;
                    }
                }

                [JsonProperty("AT")]
                public int AT
                {
                    get
                    {
                        return _AT;
                    }
                    set
                    {
                        _AT = value;
                    }
                }

                [JsonProperty("AU")]
                public int AU
                {
                    get
                    {
                        return _AU;
                    }
                    set
                    {
                        _AU = value;
                    }
                }

                [JsonProperty("BE")]
                public int BE
                {
                    get
                    {
                        return _BE;
                    }
                    set
                    {
                        _BE = value;
                    }
                }

                [JsonProperty("BG")]
                public int BG
                {
                    get
                    {
                        return _BG;
                    }
                    set
                    {
                        _BG = value;
                    }
                }

                [JsonProperty("BH")]
                public int BH
                {
                    get
                    {
                        return _BH;
                    }
                    set
                    {
                        _BH = value;
                    }
                }

                [JsonProperty("BO")]
                public int BO
                {
                    get
                    {
                        return _BO;
                    }
                    set
                    {
                        _BO = value;
                    }
                }

                [JsonProperty("BR")]
                public int BR
                {
                    get
                    {
                        return _BR;
                    }
                    set
                    {
                        _BR = value;
                    }
                }

                [JsonProperty("CA")]
                public int CA
                {
                    get
                    {
                        return _CA;
                    }
                    set
                    {
                        _CA = value;
                    }
                }

                [JsonProperty("CH")]
                public int CH
                {
                    get
                    {
                        return _CH;
                    }
                    set
                    {
                        _CH = value;
                    }
                }

                [JsonProperty("CL")]
                public int CL
                {
                    get
                    {
                        return _CL;
                    }
                    set
                    {
                        _CL = value;
                    }
                }

                [JsonProperty("CN")]
                public int CN
                {
                    get
                    {
                        return _CN;
                    }
                    set
                    {
                        _CN = value;
                    }
                }

                [JsonProperty("CO")]
                public int CO
                {
                    get
                    {
                        return _CO;
                    }
                    set
                    {
                        _CO = value;
                    }
                }

                [JsonProperty("CR")]
                public int CR
                {
                    get
                    {
                        return _CR;
                    }
                    set
                    {
                        _CR = value;
                    }
                }

                [JsonProperty("CY")]
                public int CY
                {
                    get
                    {
                        return _CY;
                    }
                    set
                    {
                        _CY = value;
                    }
                }

                [JsonProperty("CZ")]
                public int CZ
                {
                    get
                    {
                        return _CZ;
                    }
                    set
                    {
                        _CZ = value;
                    }
                }

                [JsonProperty("DE")]
                public int DE
                {
                    get
                    {
                        return _DE;
                    }
                    set
                    {
                        _DE = value;
                    }
                }

                [JsonProperty("DK")]
                public int DK
                {
                    get
                    {
                        return _DK;
                    }
                    set
                    {
                        _DK = value;
                    }
                }

                [JsonProperty("EC")]
                public int EC
                {
                    get
                    {
                        return _EC;
                    }
                    set
                    {
                        _EC = value;
                    }
                }

                [JsonProperty("ES")]
                public int ES
                {
                    get
                    {
                        return _ES;
                    }
                    set
                    {
                        _ES = value;
                    }
                }

                [JsonProperty("FI")]
                public int FI
                {
                    get
                    {
                        return _FI;
                    }
                    set
                    {
                        _FI = value;
                    }
                }

                [JsonProperty("FR")]
                public int FR
                {
                    get
                    {
                        return _FR;
                    }
                    set
                    {
                        _FR = value;
                    }
                }

                [JsonProperty("GB")]
                public int GB
                {
                    get
                    {
                        return _GB;
                    }
                    set
                    {
                        _GB = value;
                    }
                }

                [JsonProperty("GR")]
                public int GR
                {
                    get
                    {
                        return _GR;
                    }
                    set
                    {
                        _GR = value;
                    }
                }

                [JsonProperty("GT")]
                public int GT
                {
                    get
                    {
                        return _GT;
                    }
                    set
                    {
                        _GT = value;
                    }
                }

                [JsonProperty("HK")]
                public int HK
                {
                    get
                    {
                        return _HK;
                    }
                    set
                    {
                        _HK = value;
                    }
                }

                [JsonProperty("HN")]
                public int HN
                {
                    get
                    {
                        return _HN;
                    }
                    set
                    {
                        _HN = value;
                    }
                }

                [JsonProperty("HR")]
                public int HR
                {
                    get
                    {
                        return _HR;
                    }
                    set
                    {
                        _HR = value;
                    }
                }

                [JsonProperty("HU")]
                public int HU
                {
                    get
                    {
                        return _HU;
                    }
                    set
                    {
                        _HU = value;
                    }
                }

                [JsonProperty("ID")]
                public int ID
                {
                    get
                    {
                        return _ID;
                    }
                    set
                    {
                        _ID = value;
                    }
                }

                [JsonProperty("IE")]
                public int IE
                {
                    get
                    {
                        return _IE;
                    }
                    set
                    {
                        _IE = value;
                    }
                }

                [JsonProperty("IL")]
                public int IL
                {
                    get
                    {
                        return _IL;
                    }
                    set
                    {
                        _IL = value;
                    }
                }

                [JsonProperty("IN")]
                public int India
                {
                    get
                    {
                        return _India;
                    }
                    set
                    {
                        _India = value;
                    }
                }

                [JsonProperty("IS")]
                public int Iceland
                {
                    get
                    {
                        return _Iceland;
                    }
                    set
                    {
                        _Iceland = value;
                    }
                }

                [JsonProperty("IT")]
                public int IT
                {
                    get
                    {
                        return _IT;
                    }
                    set
                    {
                        _IT = value;
                    }
                }

                [JsonProperty("JP")]
                public int JP
                {
                    get
                    {
                        return _JP;
                    }
                    set
                    {
                        _JP = value;
                    }
                }

                [JsonProperty("KR")]
                public int KR
                {
                    get
                    {
                        return _KR;
                    }
                    set
                    {
                        _KR = value;
                    }
                }

                [JsonProperty("KW")]
                public int KW
                {
                    get
                    {
                        return _KW;
                    }
                    set
                    {
                        _KW = value;
                    }
                }

                [JsonProperty("LB")]
                public int LB
                {
                    get
                    {
                        return _LB;
                    }
                    set
                    {
                        _LB = value;
                    }
                }

                [JsonProperty("LU")]
                public int LU
                {
                    get
                    {
                        return _LU;
                    }
                    set
                    {
                        _LU = value;
                    }
                }

                [JsonProperty("MT")]
                public int MT
                {
                    get
                    {
                        return _MT;
                    }
                    set
                    {
                        _MT = value;
                    }
                }

                [JsonProperty("MX")]
                public int MX
                {
                    get
                    {
                        return _MX;
                    }
                    set
                    {
                        _MX = value;
                    }
                }

                [JsonProperty("MY")]
                public int MY
                {
                    get
                    {
                        return _MY;
                    }
                    set
                    {
                        _MY = value;
                    }
                }

                [JsonProperty("NI")]
                public int NI
                {
                    get
                    {
                        return _NI;
                    }
                    set
                    {
                        _NI = value;
                    }
                }

                [JsonProperty("NL")]
                public int NL
                {
                    get
                    {
                        return _NL;
                    }
                    set
                    {
                        _NL = value;
                    }
                }

                [JsonProperty("NO")]
                public int NO
                {
                    get
                    {
                        return _NO;
                    }
                    set
                    {
                        _NO = value;
                    }
                }

                [JsonProperty("NZ")]
                public int NZ
                {
                    get
                    {
                        return _NZ;
                    }
                    set
                    {
                        _NZ = value;
                    }
                }

                [JsonProperty("OM")]
                public int OM
                {
                    get
                    {
                        return _OM;
                    }
                    set
                    {
                        _OM = value;
                    }
                }

                [JsonProperty("PA")]
                public int PA
                {
                    get
                    {
                        return _PA;
                    }
                    set
                    {
                        _PA = value;
                    }
                }

                [JsonProperty("PE")]
                public int PE
                {
                    get
                    {
                        return _PE;
                    }
                    set
                    {
                        _PE = value;
                    }
                }

                [JsonProperty("PL")]
                public int PL
                {
                    get
                    {
                        return _PL;
                    }
                    set
                    {
                        _PL = value;
                    }
                }

                [JsonProperty("PT")]
                public int PT
                {
                    get
                    {
                        return _PT;
                    }
                    set
                    {
                        _PT = value;
                    }
                }

                [JsonProperty("PY")]
                public int PY
                {
                    get
                    {
                        return _PY;
                    }
                    set
                    {
                        _PY = value;
                    }
                }

                [JsonProperty("QA")]
                public int QA
                {
                    get
                    {
                        return _QA;
                    }
                    set
                    {
                        _QA = value;
                    }
                }

                [JsonProperty("RO")]
                public int RO
                {
                    get
                    {
                        return _RO;
                    }
                    set
                    {
                        _RO = value;
                    }
                }

                [JsonProperty("RU")]
                public int RU
                {
                    get
                    {
                        return _RU;
                    }
                    set
                    {
                        _RU = value;
                    }
                }

                [JsonProperty("SA")]
                public int SA
                {
                    get
                    {
                        return _SA;
                    }
                    set
                    {
                        _SA = value;
                    }
                }

                [JsonProperty("SE")]
                public int SE
                {
                    get
                    {
                        return _SE;
                    }
                    set
                    {
                        _SE = value;
                    }
                }

                [JsonProperty("SG")]
                public int SG
                {
                    get
                    {
                        return _SG;
                    }
                    set
                    {
                        _SG = value;
                    }
                }

                [JsonProperty("SI")]
                public int SI
                {
                    get
                    {
                        return _SI;
                    }
                    set
                    {
                        _SI = value;
                    }
                }

                [JsonProperty("SK")]
                public int SK
                {
                    get
                    {
                        return _SK;
                    }
                    set
                    {
                        _SK = value;
                    }
                }

                [JsonProperty("SV")]
                public int SV
                {
                    get
                    {
                        return _SV;
                    }
                    set
                    {
                        _SV = value;
                    }
                }

                [JsonProperty("TH")]
                public int TH
                {
                    get
                    {
                        return _TH;
                    }
                    set
                    {
                        _TH = value;
                    }
                }

                [JsonProperty("TR")]
                public int TR
                {
                    get
                    {
                        return _TR;
                    }
                    set
                    {
                        _TR = value;
                    }
                }

                [JsonProperty("TW")]
                public int TW
                {
                    get
                    {
                        return _TW;
                    }
                    set
                    {
                        _TW = value;
                    }
                }

                [JsonProperty("UA")]
                public int UA
                {
                    get
                    {
                        return _UA;
                    }
                    set
                    {
                        _UA = value;
                    }
                }

                [JsonProperty("UY")]
                public int UY
                {
                    get
                    {
                        return _UY;
                    }
                    set
                    {
                        _UY = value;
                    }
                }

                [JsonProperty("ZA")]
                public int ZA
                {
                    get
                    {
                        return _ZA;
                    }
                    set
                    {
                        _ZA = value;
                    }
                }

                [JsonProperty("default")]
                public int Default
                {
                    get
                    {
                        return _Default;
                    }
                    set
                    {
                        _Default = value;
                    }
                }
            }

            public class ArAE
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class CsCZ
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class DaDK
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class DeDE
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class ElGR
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class EnGB
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class EnUS
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class Es419
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class EsES
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class FiFI
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class FrCA
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class FrFR
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class HuHU
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class IdID
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class ItIT
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class JaJP
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class KoKR
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class NlNL
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class NoNO
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class PlPL
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class PtBR
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class PtPT
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class RoRO
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class RuRU
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class SvSE
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class ThTH
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class TrTR
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class ViVN
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class ZhHans
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class ZhHant
            {
                private string _TitleName;

                [JsonProperty("titleName")]
                public string TitleName
                {
                    get
                    {
                        return _TitleName;
                    }
                    set
                    {
                        _TitleName = value;
                    }
                }
            }

            public class LocalizedParameters
            {
                private ArAE _ArAE;
                private CsCZ _CsCZ;
                private DaDK _DaDK;
                private DeDE _DeDE;
                private string _DefaultLanguage;
                private ElGR _ElGR;
                private FrCA _FrCA;
                private FiFI _FiFI;
                private EsES _EsES;
                private Es419 _Es419;
                private EnUS _EnUS;
                private EnGB _EnGB;
                private PtBR _PtBR;
                private PlPL _PlPL;
                private NoNO _NoNO;
                private NlNL _NlNL;
                private KoKR _KoKR;
                private JaJP _JaJP;
                private ItIT _ItIT;
                private IdID _IdID;
                private HuHU _HuHU;
                private FrFR _FrFR;
                private ZhHant _ZhHant;
                private ZhHans _ZhHans;
                private ViVN _ViVN;
                private TrTR _TrTR;
                private ThTH _ThTH;
                private SvSE _SvSE;
                private RuRU _RuRU;
                private RoRO _RoRO;
                private PtPT _PtPT;

                [JsonProperty("ar-AE")]
                public ArAE ArAE
                {
                    get
                    {
                        return _ArAE;
                    }
                    set
                    {
                        _ArAE = value;
                    }
                }

                [JsonProperty("cs-CZ")]
                public CsCZ CsCZ
                {
                    get
                    {
                        return _CsCZ;
                    }
                    set
                    {
                        _CsCZ = value;
                    }
                }

                [JsonProperty("da-DK")]
                public DaDK DaDK
                {
                    get
                    {
                        return _DaDK;
                    }
                    set
                    {
                        _DaDK = value;
                    }
                }

                [JsonProperty("de-DE")]
                public DeDE DeDE
                {
                    get
                    {
                        return _DeDE;
                    }
                    set
                    {
                        _DeDE = value;
                    }
                }

                [JsonProperty("defaultLanguage")]
                public string DefaultLanguage
                {
                    get
                    {
                        return _DefaultLanguage;
                    }
                    set
                    {
                        _DefaultLanguage = value;
                    }
                }

                [JsonProperty("el-GR")]
                public ElGR ElGR
                {
                    get
                    {
                        return _ElGR;
                    }
                    set
                    {
                        _ElGR = value;
                    }
                }

                [JsonProperty("en-GB")]
                public EnGB EnGB
                {
                    get
                    {
                        return _EnGB;
                    }
                    set
                    {
                        _EnGB = value;
                    }
                }

                [JsonProperty("en-US")]
                public EnUS EnUS
                {
                    get
                    {
                        return _EnUS;
                    }
                    set
                    {
                        _EnUS = value;
                    }
                }

                [JsonProperty("es-419")]
                public Es419 Es419
                {
                    get
                    {
                        return _Es419;
                    }
                    set
                    {
                        _Es419 = value;
                    }
                }

                [JsonProperty("es-ES")]
                public EsES EsES
                {
                    get
                    {
                        return _EsES;
                    }
                    set
                    {
                        _EsES = value;
                    }
                }

                [JsonProperty("fi-FI")]
                public FiFI FiFI
                {
                    get
                    {
                        return _FiFI;
                    }
                    set
                    {
                        _FiFI = value;
                    }
                }

                [JsonProperty("fr-CA")]
                public FrCA FrCA
                {
                    get
                    {
                        return _FrCA;
                    }
                    set
                    {
                        _FrCA = value;
                    }
                }

                [JsonProperty("fr-FR")]
                public FrFR FrFR
                {
                    get
                    {
                        return _FrFR;
                    }
                    set
                    {
                        _FrFR = value;
                    }
                }

                [JsonProperty("hu-HU")]
                public HuHU HuHU
                {
                    get
                    {
                        return _HuHU;
                    }
                    set
                    {
                        _HuHU = value;
                    }
                }

                [JsonProperty("id-ID")]
                public IdID IdID
                {
                    get
                    {
                        return _IdID;
                    }
                    set
                    {
                        _IdID = value;
                    }
                }

                [JsonProperty("it-IT")]
                public ItIT ItIT
                {
                    get
                    {
                        return _ItIT;
                    }
                    set
                    {
                        _ItIT = value;
                    }
                }

                [JsonProperty("ja-JP")]
                public JaJP JaJP
                {
                    get
                    {
                        return _JaJP;
                    }
                    set
                    {
                        _JaJP = value;
                    }
                }

                [JsonProperty("ko-KR")]
                public KoKR KoKR
                {
                    get
                    {
                        return _KoKR;
                    }
                    set
                    {
                        _KoKR = value;
                    }
                }

                [JsonProperty("nl-NL")]
                public NlNL NlNL
                {
                    get
                    {
                        return _NlNL;
                    }
                    set
                    {
                        _NlNL = value;
                    }
                }

                [JsonProperty("no-NO")]
                public NoNO NoNO
                {
                    get
                    {
                        return _NoNO;
                    }
                    set
                    {
                        _NoNO = value;
                    }
                }

                [JsonProperty("pl-PL")]
                public PlPL PlPL
                {
                    get
                    {
                        return _PlPL;
                    }
                    set
                    {
                        _PlPL = value;
                    }
                }

                [JsonProperty("pt-BR")]
                public PtBR PtBR
                {
                    get
                    {
                        return _PtBR;
                    }
                    set
                    {
                        _PtBR = value;
                    }
                }

                [JsonProperty("pt-PT")]
                public PtPT PtPT
                {
                    get
                    {
                        return _PtPT;
                    }
                    set
                    {
                        _PtPT = value;
                    }
                }

                [JsonProperty("ro-RO")]
                public RoRO RoRO
                {
                    get
                    {
                        return _RoRO;
                    }
                    set
                    {
                        _RoRO = value;
                    }
                }

                [JsonProperty("ru-RU")]
                public RuRU RuRU
                {
                    get
                    {
                        return _RuRU;
                    }
                    set
                    {
                        _RuRU = value;
                    }
                }

                [JsonProperty("sv-SE")]
                public SvSE SvSE
                {
                    get
                    {
                        return _SvSE;
                    }
                    set
                    {
                        _SvSE = value;
                    }
                }

                [JsonProperty("th-TH")]
                public ThTH ThTH
                {
                    get
                    {
                        return _ThTH;
                    }
                    set
                    {
                        _ThTH = value;
                    }
                }

                [JsonProperty("tr-TR")]
                public TrTR TrTR
                {
                    get
                    {
                        return _TrTR;
                    }
                    set
                    {
                        _TrTR = value;
                    }
                }

                [JsonProperty("vi-VN")]
                public ViVN ViVN
                {
                    get
                    {
                        return _ViVN;
                    }
                    set
                    {
                        _ViVN = value;
                    }
                }

                [JsonProperty("zh-Hans")]
                public ZhHans ZhHans
                {
                    get
                    {
                        return _ZhHans;
                    }
                    set
                    {
                        _ZhHans = value;
                    }
                }

                [JsonProperty("zh-Hant")]
                public ZhHant ZhHant
                {
                    get
                    {
                        return _ZhHant;
                    }
                    set
                    {
                        _ZhHant = value;
                    }
                }
            }

            public class Savedata
            {
                private string[] _TitleIdForTransferringPs4;

                [JsonProperty("titleIdForTransferringPs4")]
                public string[] TitleIdForTransferringPs4
                {
                    get
                    {
                        return _TitleIdForTransferringPs4;
                    }
                    set
                    {
                        _TitleIdForTransferringPs4 = value;
                    }
                }
            }

            public class Code
            {
                private string _Asa10;

                [JsonProperty("asa10")]
                public string Asa10
                {
                    get
                    {
                        return _Asa10;
                    }
                    set
                    {
                        _Asa10 = value;
                    }
                }
            }

            public class Asa
            {
                private Code _Code;
                private string[] _Sign;

                [JsonProperty("code")]
                public Code Code
                {
                    get
                    {
                        return _Code;
                    }
                    set
                    {
                        _Code = value;
                    }
                }

                [JsonProperty("sign")]
                public string[] Sign
                {
                    get
                    {
                        return _Sign;
                    }
                    set
                    {
                        _Sign = value;
                    }
                }
            }

            public class Kernel
            {
                private int _CpuPageTableSize;
                private int _FlexibleMemorySize;
                private int _GpuPageTableSize;

                [JsonProperty("cpuPageTableSize")]
                public int CpuPageTableSize
                {
                    get
                    {
                        return _CpuPageTableSize;
                    }
                    set
                    {
                        _CpuPageTableSize = value;
                    }
                }

                [JsonProperty("flexibleMemorySize")]
                public int FlexibleMemorySize
                {
                    get
                    {
                        return _FlexibleMemorySize;
                    }
                    set
                    {
                        _FlexibleMemorySize = value;
                    }
                }

                [JsonProperty("gpuPageTableSize")]
                public int GpuPageTableSize
                {
                    get
                    {
                        return _GpuPageTableSize;
                    }
                    set
                    {
                        _GpuPageTableSize = value;
                    }
                }
            }

            public class Pubtools
            {
                private string _CreationDate;
                private string _LoudnessSnd0;
                private bool _Submission;
                private string _ToolVersion;

                [JsonProperty("creationDate")]
                public string CreationDate
                {
                    get
                    {
                        return _CreationDate;
                    }
                    set
                    {
                        _CreationDate = value;
                    }
                }

                [JsonProperty("loudnessSnd0")]
                public string LoudnessSnd0
                {
                    get
                    {
                        return _LoudnessSnd0;
                    }
                    set
                    {
                        _LoudnessSnd0 = value;
                    }
                }

                [JsonProperty("submission")]
                public bool Submission
                {
                    get
                    {
                        return _Submission;
                    }
                    set
                    {
                        _Submission = value;
                    }
                }

                [JsonProperty("toolVersion")]
                public string ToolVersion
                {
                    get
                    {
                        return _ToolVersion;
                    }
                    set
                    {
                        _ToolVersion = value;
                    }
                }
            }

            public class PS5Param
            {
                private AgeLevel _AgeLevel;
                private int _ApplicationCategoryType;
                private string _ApplicationDrmType;
                private Asa _Asa;
                private int _Attribute;
                private int _Attribute2;
                private int _Attribute3;
                private string _BackgroundBasematType;
                private string _ConceptId;
                private int _ContentBadgeType;
                private string _ContentId;
                private string _ContentVersion;
                private string _DeeplinkUri;
                private int _DownloadDataSize;
                private Kernel _Kernel;
                private LocalizedParameters _LocalizedParameters;
                private string _MasterVersion;
                private string _OriginContentVersion;
                private Pubtools _Pubtools;
                private string _RequiredSystemSoftwareVersion;
                private Savedata _Savedata;
                private string _SdkVersion;
                private string _TargetContentVersion;
                private string _TitleId;
                private string _VersionFileUri;

                [JsonProperty("ageLevel")]
                public AgeLevel AgeLevel
                {
                    get
                    {
                        return _AgeLevel;
                    }
                    set
                    {
                        _AgeLevel = value;
                    }
                }

                [JsonProperty("applicationCategoryType")]
                public int ApplicationCategoryType
                {
                    get
                    {
                        return _ApplicationCategoryType;
                    }
                    set
                    {
                        _ApplicationCategoryType = value;
                    }
                }

                [JsonProperty("applicationDrmType")]
                public string ApplicationDrmType
                {
                    get
                    {
                        return _ApplicationDrmType;
                    }
                    set
                    {
                        _ApplicationDrmType = value;
                    }
                }

                [JsonProperty("asa")]
                public Asa Asa
                {
                    get
                    {
                        return _Asa;
                    }
                    set
                    {
                        _Asa = value;
                    }
                }

                [JsonProperty("attribute")]
                public int Attribute
                {
                    get
                    {
                        return _Attribute;
                    }
                    set
                    {
                        _Attribute = value;
                    }
                }

                [JsonProperty("attribute2")]
                public int Attribute2
                {
                    get
                    {
                        return _Attribute2;
                    }
                    set
                    {
                        _Attribute2 = value;
                    }
                }

                [JsonProperty("attribute3")]
                public int Attribute3
                {
                    get
                    {
                        return _Attribute3;
                    }
                    set
                    {
                        _Attribute3 = value;
                    }
                }

                [JsonProperty("backgroundBasematType")]
                public string BackgroundBasematType
                {
                    get
                    {
                        return _BackgroundBasematType;
                    }
                    set
                    {
                        _BackgroundBasematType = value;
                    }
                }

                [JsonProperty("conceptId")]
                public string ConceptId
                {
                    get
                    {
                        return _ConceptId;
                    }
                    set
                    {
                        _ConceptId = value;
                    }
                }

                [JsonProperty("contentBadgeType")]
                public int ContentBadgeType
                {
                    get
                    {
                        return _ContentBadgeType;
                    }
                    set
                    {
                        _ContentBadgeType = value;
                    }
                }

                [JsonProperty("contentId")]
                public string ContentId
                {
                    get
                    {
                        return _ContentId;
                    }
                    set
                    {
                        _ContentId = value;
                    }
                }

                [JsonProperty("contentVersion")]
                public string ContentVersion
                {
                    get
                    {
                        return _ContentVersion;
                    }
                    set
                    {
                        _ContentVersion = value;
                    }
                }

                [JsonProperty("downloadDataSize")]
                public int DownloadDataSize
                {
                    get
                    {
                        return _DownloadDataSize;
                    }
                    set
                    {
                        _DownloadDataSize = value;
                    }
                }

                [JsonProperty("deeplinkUri")]
                public string DeeplinkUri
                {
                    get
                    {
                        return _DeeplinkUri;
                    }
                    set
                    {
                        _DeeplinkUri = value;
                    }
                }

                [JsonProperty("kernel")]
                public Kernel Kernel
                {
                    get
                    {
                        return _Kernel;
                    }
                    set
                    {
                        _Kernel = value;
                    }
                }

                [JsonProperty("localizedParameters")]
                public LocalizedParameters LocalizedParameters
                {
                    get
                    {
                        return _LocalizedParameters;
                    }
                    set
                    {
                        _LocalizedParameters = value;
                    }
                }

                [JsonProperty("masterVersion")]
                public string MasterVersion
                {
                    get
                    {
                        return _MasterVersion;
                    }
                    set
                    {
                        _MasterVersion = value;
                    }
                }

                [JsonProperty("originContentVersion")]
                public string OriginContentVersion
                {
                    get
                    {
                        return _OriginContentVersion;
                    }
                    set
                    {
                        _OriginContentVersion = value;
                    }
                }

                [JsonProperty("pubtools")]
                public Pubtools Pubtools
                {
                    get
                    {
                        return _Pubtools;
                    }
                    set
                    {
                        _Pubtools = value;
                    }
                }

                [JsonProperty("requiredSystemSoftwareVersion")]
                public string RequiredSystemSoftwareVersion
                {
                    get
                    {
                        return _RequiredSystemSoftwareVersion;
                    }
                    set
                    {
                        _RequiredSystemSoftwareVersion = value;
                    }
                }

                [JsonProperty("savedata")]
                public Savedata Savedata
                {
                    get
                    {
                        return _Savedata;
                    }
                    set
                    {
                        _Savedata = value;
                    }
                }

                [JsonProperty("sdkVersion")]
                public string SdkVersion
                {
                    get
                    {
                        return _SdkVersion;
                    }
                    set
                    {
                        _SdkVersion = value;
                    }
                }

                [JsonProperty("targetContentVersion")]
                public string TargetContentVersion
                {
                    get
                    {
                        return _TargetContentVersion;
                    }
                    set
                    {
                        _TargetContentVersion = value;
                    }
                }

                [JsonProperty("titleId")]
                public string TitleId
                {
                    get
                    {
                        return _TitleId;
                    }
                    set
                    {
                        _TitleId = value;
                    }
                }

                [JsonProperty("versionFileUri")]
                public string VersionFileUri
                {
                    get
                    {
                        return _VersionFileUri;
                    }
                    set
                    {
                        _VersionFileUri = value;
                    }
                }
            }

        }

        public class PS5Game
        {
            private string? _GameTitle;
            private string? _GameID;
            private string? _GameSize;
            private string? _GameRegion;
            private string? _GameFileOrFolderPath;
            private Bitmap? _GameCoverSource;
            private Bitmap? _GameBGSource;
            private string? _GameContentID;
            private string? _GameCategory;
            private string? _GameVersion;
            private string? _GameRequiredFirmware;
            private string? _DEGameTitle;
            private string? _JPGameTitle;
            private string? _ESGameTitle;
            private string? _ITGameTitle;
            private string? _FRGameTitle;
            private Bitmap? _GameBackgroundImageBrush;
            private string? _GameSoundFile;
            private string? _IsCompatibleFW;
            private string? _DecFilesIncluded;
            private string? _GameContentIDs;
            private string? _GameBackupType;

            public string GameTitle
            {
                get
                {
                    return _GameTitle;
                }
                set
                {
                    _GameTitle = value;
                }
            }

            public string GameID
            {
                get
                {
                    return _GameID;
                }
                set
                {
                    _GameID = value;
                }
            }

            public string GameSize
            {
                get
                {
                    return _GameSize;
                }
                set
                {
                    _GameSize = value;
                }
            }

            public string GameRegion
            {
                get
                {
                    return _GameRegion;
                }
                set
                {
                    _GameRegion = value;
                }
            }

            public string GameFileOrFolderPath
            {
                get
                {
                    return _GameFileOrFolderPath;
                }
                set
                {
                    _GameFileOrFolderPath = value;
                }
            }

            public Bitmap GameCoverSource
            {
                get
                {
                    return _GameCoverSource;
                }
                set
                {
                    _GameCoverSource = value;
                }
            }

            public Bitmap GameBGSource
            {
                get
                {
                    return _GameBGSource;
                }
                set
                {
                    _GameBGSource = value;
                }
            }

            public Bitmap GameBackgroundImageBrush
            {
                get
                {
                    return _GameBackgroundImageBrush;
                }
                set
                {
                    _GameBackgroundImageBrush = value;
                }
            }

            public string GameContentID
            {
                get
                {
                    return _GameContentID;
                }
                set
                {
                    _GameContentID = value;
                }
            }

            public string GameCategory
            {
                get
                {
                    return _GameCategory;
                }
                set
                {
                    _GameCategory = value;
                }
            }

            public string GameVersion
            {
                get
                {
                    return _GameVersion;
                }
                set
                {
                    _GameVersion = value;
                }
            }

            public string GameRequiredFirmware
            {
                get
                {
                    return _GameRequiredFirmware;
                }
                set
                {
                    _GameRequiredFirmware = value;
                }
            }

            public string GameSoundFile
            {
                get
                {
                    return _GameSoundFile;
                }
                set
                {
                    _GameSoundFile = value;
                }
            }

            public string DEGameTitle
            {
                get
                {
                    return _DEGameTitle;
                }
                set
                {
                    _DEGameTitle = value;
                }
            }

            public string FRGameTitle
            {
                get
                {
                    return _FRGameTitle;
                }
                set
                {
                    _FRGameTitle = value;
                }
            }

            public string ITGameTitle
            {
                get
                {
                    return _ITGameTitle;
                }
                set
                {
                    _ITGameTitle = value;
                }
            }

            public string ESGameTitle
            {
                get
                {
                    return _ESGameTitle;
                }
                set
                {
                    _ESGameTitle = value;
                }
            }

            public string JPGameTitle
            {
                get
                {
                    return _JPGameTitle;
                }
                set
                {
                    _JPGameTitle = value;
                }
            }

            public string IsCompatibleFW
            {
                get
                {
                    return _IsCompatibleFW;
                }
                set
                {
                    _IsCompatibleFW = value;
                }
            }

            public string DecFilesIncluded
            {
                get
                {
                    return _DecFilesIncluded;
                }
                set
                {
                    _DecFilesIncluded = value;
                }
            }

            public string GameContentIDs
            {
                get
                {
                    return _GameContentIDs;
                }
                set
                {
                    _GameContentIDs = value;
                }
            }

            public string GameBackupType
            {
                get
                {
                    return _GameBackupType;
                }
                set
                {
                    _GameBackupType = value;
                }
            }

            public static string GetGameRegion(string GameID)
            {
                if (GameID.StartsWith("PPSA"))
                {
                    return "NA / Europe";
                }
                else if (GameID.StartsWith("ECAS"))
                {
                    return "Asia";
                }
                else if (GameID.StartsWith("ELAS"))
                {
                    return "Asia";
                }
                else if (GameID.StartsWith("ELJM"))
                {
                    return "Japan";
                }
                else
                {
                    return "";
                }
            }

        }

        public struct PS5PKGEntry
        {
            public string EntryOffset { get; set; }

            public string EntrySize { get; set; }

            public string EntryName { get; set; }
        }

        public struct PS5PKGScenario
        {
            private string _ScenarioName;
            private string _ScenarioType;
            private string _ScenarioID;

            public string ScenarioID
            {
                get
                {
                    return _ScenarioID;
                }
                set
                {
                    _ScenarioID = value;
                }
            }

            public string ScenarioType
            {
                get
                {
                    return _ScenarioType;
                }
                set
                {
                    _ScenarioType = value;
                }
            }

            public string ScenarioName
            {
                get
                {
                    return _ScenarioName;
                }
                set
                {
                    _ScenarioName = value;
                }
            }
        }

        public struct PS5PKGChunk
        {
            private string _ChunkID;
            private string _ChunkFlag;
            private string _ChunkLocus;
            private string _ChunkName;
            private string _ChunkSize;
            private string _ChunkNum;
            private string _ChunkDisps;
            private string _ChunkLanguage;
            private string _ChunkValue;

            public string ChunkID
            {
                get
                {
                    return _ChunkID;
                }
                set
                {
                    _ChunkID = value;
                }
            }

            public string ChunkFlag
            {
                get
                {
                    return _ChunkFlag;
                }
                set
                {
                    _ChunkFlag = value;
                }
            }

            public string ChunkLocus
            {
                get
                {
                    return _ChunkLocus;
                }
                set
                {
                    _ChunkLocus = value;
                }
            }

            public string ChunkLanguage
            {
                get
                {
                    return _ChunkLanguage;
                }
                set
                {
                    _ChunkLanguage = value;
                }
            }

            public string ChunkDisps
            {
                get
                {
                    return _ChunkDisps;
                }
                set
                {
                    _ChunkDisps = value;
                }
            }

            public string ChunkNum
            {
                get
                {
                    return _ChunkNum;
                }
                set
                {
                    _ChunkNum = value;
                }
            }

            public string ChunkSize
            {
                get
                {
                    return _ChunkSize;
                }
                set
                {
                    _ChunkSize = value;
                }
            }

            public string ChunkName
            {
                get
                {
                    return _ChunkName;
                }
                set
                {
                    _ChunkName = value;
                }
            }

            public string ChunkValue
            {
                get
                {
                    return _ChunkValue;
                }
                set
                {
                    _ChunkValue = value;
                }
            }
        }

        public struct PS5PKGOuter
        {
            private string _OuterChunks;
            private string _OuterSize;
            private string _OuterOffset;
            private string _OuterImage;
            private string _OuterID;

            public string OuterID
            {
                get
                {
                    return _OuterID;
                }
                set
                {
                    _OuterID = value;
                }
            }

            public string OuterImage
            {
                get
                {
                    return _OuterImage;
                }
                set
                {
                    _OuterImage = value;
                }
            }

            public string OuterOffset
            {
                get
                {
                    return _OuterOffset;
                }
                set
                {
                    _OuterOffset = value;
                }
            }

            public string OuterSize
            {
                get
                {
                    return _OuterSize;
                }
                set
                {
                    _OuterSize = value;
                }
            }

            public string OuterChunks
            {
                get
                {
                    return _OuterChunks;
                }
                set
                {
                    _OuterChunks = value;
                }
            }
        }

        public struct PS5PKGRootDirectory
        {
            private string _DirectoryName;
            private string _DirectoryINode;
            private string _DirectoryIndex;
            private string _DirectoryIMode;
            private string _DirectoryLinks;
            private string _DirectorySize;

            public string DirectorySize
            {
                get
                {
                    return _DirectorySize;
                }
                set
                {
                    _DirectorySize = value;
                }
            }

            public string DirectoryLinks
            {
                get
                {
                    return _DirectoryLinks;
                }
                set
                {
                    _DirectoryLinks = value;
                }
            }

            public string DirectoryIMode
            {
                get
                {
                    return _DirectoryIMode;
                }
                set
                {
                    _DirectoryIMode = value;
                }
            }

            public string DirectoryIndex
            {
                get
                {
                    return _DirectoryIndex;
                }
                set
                {
                    _DirectoryIndex = value;
                }
            }

            public string DirectoryINode
            {
                get
                {
                    return _DirectoryINode;
                }
                set
                {
                    _DirectoryINode = value;
                }
            }

            public string DirectoryName
            {
                get
                {
                    return _DirectoryName;
                }
                set
                {
                    _DirectoryName = value;
                }
            }
        }

        public struct PS5PKGRootFile
        {
            private string _FileName;
            private string _FileINode;
            private string _FileIndex;
            private string _FileIMode;
            private string _FileCompression;
            private string _FilePlain;
            private string _FileSize;

            public string FileSize
            {
                get
                {
                    return _FileSize;
                }
                set
                {
                    _FileSize = value;
                }
            }

            public string FilePlain
            {
                get
                {
                    return _FilePlain;
                }
                set
                {
                    _FilePlain = value;
                }
            }

            public string FileCompression
            {
                get
                {
                    return _FileCompression;
                }
                set
                {
                    _FileCompression = value;
                }
            }

            public string FileIMode
            {
                get
                {
                    return _FileIMode;
                }
                set
                {
                    _FileIMode = value;
                }
            }

            public string FileIndex
            {
                get
                {
                    return _FileIndex;
                }
                set
                {
                    _FileIndex = value;
                }
            }

            public string FileINode
            {
                get
                {
                    return _FileINode;
                }
                set
                {
                    _FileINode = value;
                }
            }

            public string FileName
            {
                get
                {
                    return _FileName;
                }
                set
                {
                    _FileName = value;
                }
            }
        }

        #endregion

    }
}