using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using IronSoftware.Drawing;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using static PS5PKGReader.PKGReader;
using static PS5PKGReader.PS5ParamClass;

namespace PS5PKGReader
{

#pragma warning disable CS0618 // Type or member is obsolete

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public List<PS5PKGRootFile> PFSImageRootFiles = [];
        public List<PS5PKGRootDirectory> PFSImageRootDirectories = [];
        public List<PS5PKGRootFile> PFSImageURootFiles = [];
        public List<PS5PKGRootFile> NestedImageRootFiles = [];
        public List<PS5PKGRootDirectory> NestedImageRootDirectories = [];
        public List<PS5PKGRootFile> NestedImageURootFiles = [];

        private string CurrentParamJSON = "";
        private XDocument? CurrentConfigurationXML = null;
        private AnyBitmap? CurrentIcon0 = null;
        private AnyBitmap? CurrentPic0 = null;
        private byte[]? CurrentSnd0 = null;

        private async void BrowsePKGFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (VisualRoot is not Window window)
                return;


            var newOpenFileDialog = new OpenFileDialog() { Title = "Select a PS5 PKG file :", AllowMultiple = false, Filters = [new FileDialogFilter() { Name = "PKG files", Extensions = { "pkg" } }] };
            var selectedFile = await newOpenFileDialog.ShowAsync(window);

            if (selectedFile is null || selectedFile.Length == 0)
                return;
            if (selectedFile[0] != null)
            {
                SelectedPKGFileTextBox.Text = selectedFile[0];

                // Reset
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

                PKGIconImage.Source = null;
                CurrentParamJSON = "";
                CurrentConfigurationXML = null;
                CurrentIcon0 = null;
                CurrentPic0 = null;
                ShowPKGPFSImageFilesButton.IsEnabled = false;
                ShowPKGNestedImageFilesButton.IsEnabled = false;
                ShowPKGPFSImageDirectoriesButton.IsEnabled = false;
                ShowPKGNestedImageDirectoriesButton.IsEnabled = false;
                ShowPKGEntriesButton.IsEnabled = false;
                ShowPKGScenariosButton.IsEnabled = false;
                ShowPKGChunksButton.IsEnabled = false;
                ShowPKGOutersButton.IsEnabled = false;
                ExportConfigurationXMLButton.IsEnabled = false;
                ExportParamJSONButton.IsEnabled = false;
                ExportIcon0PNGButton.IsEnabled = false;
                ExportPic0Button.IsEnabled = false;
                ExportSnd0Button.IsEnabled = false;

                // Determine PS5 PKG
                var PKGType = PS5PKG.GetPKGType(selectedFile[0]);
                if (PKGType != null)
                {
                    if (PKGType == PS5PKG.PKGType.Meta | PKGType == PS5PKG.PKGType.FullRetail)
                    {
                        var ParamData = PS5PKG.GetRetailPKGParamJSON(selectedFile[0]);
                        var (PKGBackgrounds, PKGIcons) = PS5PKG.GetRetailPKGPNGs(selectedFile[0]);

                        if (ParamData != null)
                        {
                            var NewPS5Game = new PS5Game();
                            if (ParamData.TitleId != null)
                            {
                                NewPS5Game.GameID = "Title ID: " + ParamData.TitleId;
                                NewPS5Game.GameRegion = "Region: " + PS5Game.GetGameRegion(ParamData.TitleId);
                            }

                            bool NonENUSTitle = false;
                            if (ParamData.LocalizedParameters!.EnUS != null)
                            {
                                NewPS5Game.GameTitle = ParamData.LocalizedParameters.EnUS.TitleName;
                            }
                            else
                            {
                                NonENUSTitle = true;
                            }
                            if (ParamData.LocalizedParameters.DeDE != null)
                            {
                                NewPS5Game.DEGameTitle = ParamData.LocalizedParameters.DeDE.TitleName;
                            }
                            if (ParamData.LocalizedParameters.FrFR != null)
                            {
                                NewPS5Game.FRGameTitle = ParamData.LocalizedParameters.FrFR.TitleName;
                            }
                            if (ParamData.LocalizedParameters.ItIT != null)
                            {
                                NewPS5Game.ITGameTitle = ParamData.LocalizedParameters.ItIT.TitleName;
                            }
                            if (ParamData.LocalizedParameters.EsES != null)
                            {
                                NewPS5Game.ESGameTitle = ParamData.LocalizedParameters.EsES.TitleName;
                            }
                            if (ParamData.LocalizedParameters.JaJP != null)
                            {
                                NewPS5Game.JPGameTitle = ParamData.LocalizedParameters.JaJP.TitleName;
                            }
                            if (NonENUSTitle)
                            {
                                NewPS5Game.GameTitle = GetAlternativeGameTitle(ParamData);
                            }

                            if (ParamData.ContentId != null)
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

                            long PS5GameSize = new FileInfo(selectedFile[0]).Length;
                            NewPS5Game.GameSize = $"Size: {HumanReadableBytes(PS5GameSize)}";

                            if (ParamData.ContentVersion != null)
                            {
                                NewPS5Game.GameVersion = "Version: " + ParamData.ContentVersion;
                            }
                            if (ParamData.RequiredSystemSoftwareVersion != null)
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

                        if (PKGIcons.Count > 0)
                        {
                            var icon0Data = PKGIcons[0].FileData;
                            if (icon0Data != null && icon0Data.Length > 0)
                            {
                                AnyBitmap Icon0BitmapImage = AnyBitmap.FromBytes(icon0Data, true);
                                PKGIconImage.Source = AnyBitmapToIImage(Icon0BitmapImage);
                                CurrentIcon0 = Icon0BitmapImage;
                                ExportIcon0PNGButton.IsEnabled = true;
                            }
                        }

                        if (PKGBackgrounds.Count > 0)
                        {
                            var pic0Data = PKGBackgrounds[0].FileData;
                            AnyBitmap Pic0BitmapImage = AnyBitmap.FromBytes(pic0Data, true);
                            CurrentPic0 = Pic0BitmapImage;
                            ExportPic0Button.IsEnabled = true;
                        }

                        if (ParamData == null && PKGIcons.Count == 0 && PKGBackgrounds.Count == 0)
                        {
                            var box = MessageBoxManager.GetMessageBoxStandard("No Metadata found", "Selected PKG could not be read and might be corrupt.", ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
                            await box.ShowWindowAsync();
                        }
                        return;
                    }
                    else if (PKGType == PS5PKG.PKGType.FullDebug)
                    {
                        string ExtractedPKGConfigurationData = "";
                        string PKGConfigurationStartString = "<package-configuration version=\"1.0\" type=\"package-info\">";
                        string PKGConfigurationEndString = "</package-configuration>";

                        byte[] PKGConfigurationStartBytes = Encoding.UTF8.GetBytes(PKGConfigurationStartString);
                        byte[] PKGConfigurationEndBytes = Encoding.UTF8.GetBytes(PKGConfigurationEndString);

                        long PKGConfigurationStartOffset = -1;
                        long PKGConfigurationEndOffset = -1;

                        // 1. Get the PKG configuration
                        using (var NewFileStream = new FileStream(selectedFile[0], FileMode.Open, FileAccess.Read))
                        {
                            var buffer = new byte[4097];
                            long fileLength = NewFileStream.Length;
                            long totalBytesRead = fileLength;

                            // Read backwards to find the end string first
                            while (totalBytesRead > 0L)
                            {
                                int bytesRead = (int)Math.Min(buffer.Length, totalBytesRead);
                                NewFileStream.Seek(totalBytesRead - bytesRead, SeekOrigin.Begin);
                                NewFileStream.ReadExactly(buffer, 0, bytesRead);
                                totalBytesRead -= bytesRead;

                                // Check buffer from end to start
                                bool exitWhile1 = false;
                                for (int i = bytesRead - 1; i >= 0; i -= 1)
                                {
                                    // Check for end string
                                    if (PKGConfigurationEndOffset == -1 && PS5PKG.MatchBytes(buffer, i, PKGConfigurationEndBytes))
                                    {
                                        PKGConfigurationEndOffset = totalBytesRead + i + PKGConfigurationEndBytes.Length;
                                    }

                                    // Check for start string
                                    if (PKGConfigurationStartOffset == -1 && PS5PKG.MatchBytes(buffer, i, PKGConfigurationStartBytes))
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
                                NewFileStream.Seek(PKGConfigurationStartOffset, SeekOrigin.Begin);

                                // Read the pkg configuration data
                                var data = new byte[((int)PKGConfigurationDataSize)];
                                NewFileStream.ReadExactly(data, 0, (int)PKGConfigurationDataSize);

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

                            if (PKGConfigurationXML != null)
                            {
                                ExportConfigurationXMLButton.IsEnabled = true;

                                // Get the PKG config values
                                var PKGConfig = PKGConfigurationXML.Element("config");
                                if (PKGConfig != null)
                                {
                                    string PKGConfigVersion = PKGConfig.Attribute("version")?.Value ?? "";
                                    string PKGConfigMetadata = PKGConfig.Attribute("metadata")?.Value ?? "";
                                    string PKGConfigPrimary = PKGConfig.Attribute("primary")?.Value ?? "";
                                }
                                string PKGConfigContentID = PKGConfigurationXML.Descendants("config").First().Element("content-id")?.Value ?? "";
                                string PKGConfigPrimaryID = PKGConfigurationXML.Descendants("config").First().Element("primary-id")?.Value ?? "";
                                string PKGConfigLongName = PKGConfigurationXML.Descendants("config").First().Element("longname")?.Value ?? "";
                                string PKGConfigRequiredSystemVersion = PKGConfigurationXML.Descendants("config").First().Element("required-system-version")?.Value ?? "";
                                string PKGConfigDRMType = PKGConfigurationXML.Descendants("config").First().Element("drm-type")?.Value ?? "";
                                string PKGConfigContentType = PKGConfigurationXML.Descendants("config").First().Element("content-type")?.Value ?? "";
                                string PKGConfigApplicationType = PKGConfigurationXML.Descendants("config").First().Element("application-type")?.Value ?? "";
                                string PKGConfigNumberOfImages = PKGConfigurationXML.Descendants("config").First().Element("num-of-images")?.Value ?? "";
                                string PKGConfigSize = PKGConfigurationXML.Descendants("config").First().Element("package-size")?.Value ?? "";
                                string PKGConfigVersionDate = PKGConfigurationXML.Descendants("config").First().Element("version-date")?.Value ?? "";
                                string PKGConfigVersionHash = PKGConfigurationXML.Descendants("config").First().Element("version-hash")?.Value ?? "";

                                // Get the PKG digests
                                var PKGDigests = PKGConfigurationXML.Element("digests");
                                if (PKGDigests != null)
                                {
                                    string PKGDigestsVersion = PKGDigests.Attribute("version")?.Value ?? "";
                                    string PKGDigestsMajorParamVersion = PKGDigests.Attribute("major-param-version")?.Value ?? "";
                                }
                                string PKGContentDigest = PKGConfigurationXML.Descendants("digests").First().Element("content-digest")?.Value ?? "";
                                string PKGGameDigest = PKGConfigurationXML.Descendants("digests").First().Element("game-digest")?.Value ?? "";
                                string PKGHeaderDigest = PKGConfigurationXML.Descendants("digests").First().Element("header-digest")?.Value ?? "";
                                string PKGSystemDigest = PKGConfigurationXML.Descendants("digests").First().Element("system-digest")?.Value ?? "";
                                string PKGParamDigest = PKGConfigurationXML.Descendants("digests").First().Element("param-digest")?.Value ?? "";
                                string PKGDigest = PKGConfigurationXML.Descendants("digests").First().Element("package-digest")?.Value ?? "";

                                // Get the PKG params
                                string PKGParamApplicationDRMType = PKGConfigurationXML.Descendants("params").First().Element("applicationDrmType")?.Value ?? "";
                                string PKGParamContentID = PKGConfigurationXML.Descendants("params").First().Element("contentId")?.Value ?? "";
                                string PKGParamContentVersion = PKGConfigurationXML.Descendants("params").First().Element("contentVersion")?.Value ?? "";
                                string PKGParamMasterVersion = PKGConfigurationXML.Descendants("params").First().Element("masterVersion")?.Value ?? "";
                                string PKGParamRequiredSystemVersion = PKGConfigurationXML.Descendants("params").First().Element("requiredSystemSoftwareVersion")?.Value ?? "";
                                string PKGParamSDKVersion = PKGConfigurationXML.Descendants("params").First().Element("sdkVersion")?.Value ?? "";
                                string PKGParamTitleName = PKGConfigurationXML.Descendants("params").First().Element("titleName")?.Value ?? "";

                                // Get the PKG container information
                                string PKGContainerSize = PKGConfigurationXML.Descendants("container").First().Element("container-size")?.Value ?? "";
                                string PKGContainerMandatorySize = PKGConfigurationXML.Descendants("container").First().Element("mandatory-size")?.Value ?? "";
                                string PKGContainerBodyOffset = PKGConfigurationXML.Descendants("container").First().Element("body-offset")?.Value ?? "";
                                string PKGContainerBodySize = PKGConfigurationXML.Descendants("container").First().Element("body-size")?.Value ?? "";
                                string PKGContainerBodyDigest = PKGConfigurationXML.Descendants("container").First().Element("body-digest")?.Value ?? "";
                                string PKGContainerPromoteSize = PKGConfigurationXML.Descendants("container").First().Element("promote-size")?.Value ?? "";

                                // Get the PKG mount image
                                string PKGMountImagePFSOffsetAlign = PKGConfigurationXML.Descendants("mount-image").First().Element("pfs-offset-align")?.Value ?? "";
                                string PKGMountImagePFSSizeAlign = PKGConfigurationXML.Descendants("mount-image").First().Element("pfs-size-align")?.Value ?? "";
                                string PKGMountImagePFSImageOffset = PKGConfigurationXML.Descendants("mount-image").First().Element("pfs-image-offset")?.Value ?? "";
                                string PKGMountImagePFSImageSize = PKGConfigurationXML.Descendants("mount-image").First().Element("pfs-image-size")?.Value ?? "";
                                string PKGMountImageFixedInfoSize = PKGConfigurationXML.Descendants("mount-image").First().Element("fixed-info-size")?.Value ?? "";
                                string PKGMountImagePFSImageSeed = PKGConfigurationXML.Descendants("mount-image").First().Element("pfs-image-seed")?.Value ?? "";
                                string PKGMountImageSBlockDigest = PKGConfigurationXML.Descendants("mount-image").First().Element("sblock-digest")?.Value ?? "";
                                string PKGMountImageFixedInfoDigest = PKGConfigurationXML.Descendants("mount-image").First().Element("fixed-info-digest")?.Value ?? "";
                                string PKGMountImageOffset = PKGConfigurationXML.Descendants("mount-image").First().Element("mount-image-offset")?.Value ?? "";
                                string PKGMountImageSize = PKGConfigurationXML.Descendants("mount-image").First().Element("mount-image-size")?.Value ?? "";
                                string PKGMountImageContainerOffset = PKGConfigurationXML.Descendants("mount-image").First().Element("container-offset")?.Value ?? "";
                                string PKGMountImageSupplementalOffset = PKGConfigurationXML.Descendants("mount-image").First().Element("supplemental-offset")?.Value ?? "";

                                // Get the PKG entries and add to PKGContentListView
                                var PKGEntries = PKGConfigurationXML.Descendants("entries").Descendants("entry");
                                if (PKGEntries != null)
                                {
                                    ShowPKGEntriesButton.IsEnabled = true;

                                    foreach (XElement PKGEntry in PKGEntries)
                                    {
                                        var NewPS5PKGEntry = new PS5PKGEntry() { EntryOffset = PKGEntry.Attribute("offset")?.Value ?? "", EntrySize = PKGEntry.Attribute("size")?.Value ?? "", EntryName = PKGEntry.Attribute("name")?.Value ?? "" };
                                        PKGContentListBox.Items.Add(NewPS5PKGEntry);
                                    }
                                }

                                // Get the PKG chunkinfo
                                var PKGChunkInfo = PKGConfigurationXML.Element("chunkinfo");
                                if (PKGChunkInfo != null)
                                {
                                    string PKGChunkInfoSize = PKGChunkInfo.Attribute("size")?.Value ?? "";
                                    string PKGChunkInfoNested = PKGChunkInfo.Attribute("nested")?.Value ?? "";
                                    string PKGChunkInfoSDK = PKGChunkInfo.Attribute("sdk")?.Value ?? "";
                                    string PKGChunkInfoDisps = PKGChunkInfo.Attribute("disps")?.Value ?? "";
                                }
                                string PKGChunkInfoContentID = PKGConfigurationXML.Descendants("chunkinfo").First().Element("contentid")?.Value ?? "";
                                string PKGChunkInfoLanguages = PKGConfigurationXML.Descendants("chunkinfo").First().Element("languages")?.Value ?? "";

                                // Get the PKG chunkinfo scenarios
                                var PKGChunkInfoScenarios = PKGConfigurationXML.Descendants("chunkinfo").Descendants("scenarios").Descendants("scenario");
                                if (PKGChunkInfoScenarios != null)
                                {
                                    ShowPKGScenariosButton.IsEnabled = true;

                                    foreach (XElement PKGChunkInfoScenario in PKGChunkInfoScenarios)
                                    {
                                        var NewPS5PKGChunkInfoScenario = new PS5PKGScenario()
                                        {
                                            ScenarioID = PKGChunkInfoScenario.Attribute("id")?.Value ?? "",
                                            ScenarioType = PKGChunkInfoScenario.Attribute("type")?.Value ?? "",
                                            ScenarioName = PKGChunkInfoScenario.Attribute("name")?.Value ?? ""
                                        };
                                        PKGScenariosListBox.Items.Add(NewPS5PKGChunkInfoScenario);
                                    }
                                }

                                // Get the PKG chunkinfo chunks
                                var PKGChunkInfoChunks = PKGConfigurationXML.Element("chunks");
                                if (PKGChunkInfoChunks != null)
                                {
                                    string PKGChunkInfoChunksNum = PKGChunkInfoChunks.Attribute("num")?.Value ?? "";
                                    string PKGChunkInfoChunksDefault = PKGChunkInfoChunks.Attribute("default")?.Value ?? "";
                                }
                                var PKGChunkInfoChunksList = PKGConfigurationXML.Descendants("chunkinfo").Descendants("chunks").Descendants("chunk");
                                if (PKGChunkInfoChunksList != null)
                                {
                                    ShowPKGChunksButton.IsEnabled = true;

                                    foreach (XElement PKGChunkInfoChunk in PKGChunkInfoChunksList)
                                    {
                                        var NewPS5PKGChunkInfoChunk = new PS5PKGChunk()
                                        {
                                            ChunkID = PKGChunkInfoChunk.Attribute("id")?.Value ?? "",
                                            ChunkFlag = PKGChunkInfoChunk.Attribute("flag")?.Value ?? "",
                                            ChunkLocus = PKGChunkInfoChunk.Attribute("locus")?.Value ?? "",
                                            ChunkLanguage = PKGChunkInfoChunk.Attribute("language")?.Value ?? "",
                                            ChunkDisps = PKGChunkInfoChunk.Attribute("disps")?.Value ?? "",
                                            ChunkNum = PKGChunkInfoChunk.Attribute("num")?.Value ?? "",
                                            ChunkSize = PKGChunkInfoChunk.Attribute("size")?.Value ?? "",
                                            ChunkName = PKGChunkInfoChunk.Attribute("name")?.Value ?? "",
                                            ChunkValue = PKGChunkInfoChunk.Value
                                        };
                                        PKGChunksListBox.Items.Add(NewPS5PKGChunkInfoChunk);
                                    }
                                }

                                // Get the PKG chunkinfo outers
                                var PKGChunkInfoOuters = PKGConfigurationXML.Element("outers");
                                if (PKGChunkInfoOuters != null)
                                {
                                    string PKGChunkInfoOutersNum = PKGChunkInfoOuters.Attribute("num")?.Value ?? "";
                                    string PKGChunkInfoOutersOverlapped = PKGChunkInfoOuters.Attribute("overlapped")?.Value ?? "";
                                    string PKGChunkInfoOutersLanguageOverlapped = PKGChunkInfoOuters.Attribute("language-overlapped")?.Value ?? "";
                                }
                                var PKGChunkInfoOutersList = PKGConfigurationXML.Descendants("chunkinfo").Descendants("outers").Descendants("outer");
                                if (PKGChunkInfoOutersList != null)
                                {
                                    ShowPKGOutersButton.IsEnabled = true;

                                    foreach (XElement PKGChunkInfoOuter in PKGChunkInfoOutersList)
                                    {
                                        var NewPS5PKGOuter = new PS5PKGOuter()
                                        {
                                            OuterID = PKGChunkInfoOuter.Attribute("id")?.Value ?? "",
                                            OuterImage = PKGChunkInfoOuter.Attribute("image")?.Value ?? "",
                                            OuterOffset = PKGChunkInfoOuter.Attribute("offset")?.Value ?? "",
                                            OuterSize = PKGChunkInfoOuter.Attribute("size")?.Value ?? "",
                                            OuterChunks = PKGChunkInfoOuter.Attribute("chunks")?.Value ?? ""
                                        };
                                        PKGOutersListBox.Items.Add(NewPS5PKGOuter);
                                    }
                                }

                                // Get the PKG pfs image info
                                var PKGPFSImage = PKGConfigurationXML.Element("pfs-image");
                                if (PKGPFSImage != null)
                                {
                                    string PKGPFSImageVersion = PKGPFSImage.Attribute("version")?.Value ?? "";
                                    string PKGPFSImageReadOnly = PKGPFSImage.Attribute("readonly")?.Value ?? "";
                                    string PKGPFSImageOffset = PKGPFSImage.Attribute("offset")?.Value ?? "";
                                    string PKGPFSImageMetadata = PKGPFSImage.Attribute("metadata")?.Value ?? "";
                                }

                                // Get the PKG pfs image sblock info
                                var PKGPFSImageSBlock = PKGConfigurationXML.Descendants("sblock").FirstOrDefault();
                                if (PKGPFSImageSBlock != null)
                                {
                                    string PKGPFSImageSBlockSigned = PKGPFSImageSBlock.Attribute("signed")?.Value ?? "";
                                    string PKGPFSImageSBlockEncrypted = PKGPFSImageSBlock.Attribute("encrypted")?.Value ?? "";
                                    string PKGPFSImageSBlockIgnoreCase = PKGPFSImageSBlock.Attribute("ignore-case")?.Value ?? "";
                                    string PKGPFSImageSBlockIndexSize = PKGPFSImageSBlock.Attribute("index-size")?.Value ?? "";
                                    string PKGPFSImageSBlockBlocks = PKGPFSImageSBlock.Attribute("blocks")?.Value ?? "";
                                    string PKGPFSImageSBlockBackups = PKGPFSImageSBlock.Attribute("backups")?.Value ?? "";
                                }
                                var PKGPFSImageSBlockImageSize = PKGConfigurationXML.Descendants("sblock").FirstOrDefault()?.Element("image-size");
                                if (PKGPFSImageSBlockImageSize != null)
                                {
                                    string PKGPFSImageSBlockImageSizeBlockSize = PKGPFSImageSBlockImageSize.Attribute("block-size")?.Value ?? "";
                                    string PKGPFSImageSBlockImageSizeNum = PKGPFSImageSBlockImageSize.Attribute("num")?.Value ?? "";
                                    string PKGPFSImageSBlockImageSizeValue = PKGPFSImageSBlockImageSize.Value;
                                }
                                var PKGPFSImageSBlockSuperInode = PKGConfigurationXML.Descendants("sblock").FirstOrDefault()?.Element("super-inode");
                                if (PKGPFSImageSBlockSuperInode != null)
                                {
                                    string PKGPFSImageSBlockSuperInodeBlocks = PKGPFSImageSBlockSuperInode.Attribute("blocks")?.Value ?? "";
                                    string PKGPFSImageSBlockSuperInodeInodes = PKGPFSImageSBlockSuperInode.Attribute("inodes")?.Value ?? "";
                                    string PKGPFSImageSBlockSuperInodeRoot = PKGPFSImageSBlockSuperInode.Attribute("root")?.Value ?? "";
                                }
                                var PKGPFSImageSBlockInode = PKGConfigurationXML.Descendants("sblock").FirstOrDefault()?.Descendants("super-inode").FirstOrDefault()?.Element("inode");
                                if (PKGPFSImageSBlockInode != null)
                                {
                                    string PKGPFSImageSBlockInodeSize = PKGPFSImageSBlockInode.Attribute("size")?.Value ?? "";
                                    string PKGPFSImageSBlockInodeLinks = PKGPFSImageSBlockInode.Attribute("links")?.Value ?? "";
                                    string PKGPFSImageSBlockInodeMode = PKGPFSImageSBlockInode.Attribute("mode")?.Value ?? "";
                                    string PKGPFSImageSBlockInodeIMode = PKGPFSImageSBlockInode.Attribute("imode")?.Value ?? "";
                                    string PKGPFSImageSBlockInodeIndex = PKGPFSImageSBlockInode.Attribute("index")?.Value ?? "";
                                }
                                string PKGPFSImageSBlockSeed = PKGConfigurationXML.Descendants("sblock").FirstOrDefault()?.Element("seed")?.Value ?? "";
                                string PKGPFSImageSBlockICV = PKGConfigurationXML.Descendants("sblock").FirstOrDefault()?.Element("icv")?.Value ?? "";

                                // Get the PKG pfs image root info
                                var PKGPFSImageRoot = PKGConfigurationXML.Descendants("pfs-image").FirstOrDefault()?.Element("root");
                                if (PKGPFSImageRoot != null)
                                {
                                    string PKGPFSImageRootSize = PKGPFSImageRoot.Attribute("size")?.Value ?? "";
                                    string PKGPFSImageRootLinks = PKGPFSImageRoot.Attribute("links")?.Value ?? "";
                                    string PKGPFSImageRootIMode = PKGPFSImageRoot.Attribute("imode")?.Value ?? "";
                                    string PKGPFSImageRootIndex = PKGPFSImageRoot.Attribute("index")?.Value ?? "";
                                    string PKGPFSImageRootINode = PKGPFSImageRoot.Attribute("inode")?.Value ?? "";
                                    string PKGPFSImageRootName = PKGPFSImageRoot.Attribute("name")?.Value ?? "";
                                }

                                // Get the files in root
                                var PKGPFSImageRootFiles = PKGConfigurationXML.Descendants("pfs-image").FirstOrDefault()?.Descendants("root").FirstOrDefault()?.Descendants("file");
                                if (PKGPFSImageRootFiles != null)
                                {
                                    ShowPKGPFSImageFilesButton.IsEnabled = true;

                                    foreach (XElement PKGPFSImageRootFile in PKGPFSImageRootFiles)
                                    {
                                        if (PKGPFSImageRootFile != null)
                                        {
                                            PS5PKGRootFile NewPS5PKGRootFile = new()
                                            {
                                                FileSize = PKGPFSImageRootFile.Attribute("size")?.Value ?? "",
                                                FilePlain = PKGPFSImageRootFile.Attribute("plain")?.Value ?? "",
                                                FileCompression = PKGPFSImageRootFile.Attribute("comp")?.Value ?? "",
                                                FileIMode = PKGPFSImageRootFile.Attribute("imode")?.Value ?? "",
                                                FileIndex = PKGPFSImageRootFile.Attribute("index")?.Value ?? "",
                                                FileINode = PKGPFSImageRootFile.Attribute("inode")?.Value ?? "",
                                                FileName = PKGPFSImageRootFile.Attribute("name")?.Value ?? ""
                                            };

                                            PFSImageRootFiles.Add(NewPS5PKGRootFile);
                                        }
                                    }
                                }

                                // Get the directories in root
                                var PKGPFSImageRootDirectories = PKGConfigurationXML.Descendants("pfs-image").FirstOrDefault()?.Descendants("root").FirstOrDefault()?.Descendants("dir");
                                if (PKGPFSImageRootDirectories != null)
                                {
                                    ShowPKGPFSImageDirectoriesButton.IsEnabled = true;

                                    foreach (XElement PKGPFSImageRootDirectory in PKGPFSImageRootDirectories)
                                    {
                                        if (PKGPFSImageRootDirectory != null)
                                        {
                                            PS5PKGRootDirectory NewPS5PKGRootDirectory = new()
                                            {
                                                DirectorySize = PKGPFSImageRootDirectory.Attribute("size")?.Value ?? "",
                                                DirectoryLinks = PKGPFSImageRootDirectory.Attribute("links")?.Value ?? "",
                                                DirectoryIMode = PKGPFSImageRootDirectory.Attribute("imode")?.Value ?? "",
                                                DirectoryIndex = PKGPFSImageRootDirectory.Attribute("index")?.Value ?? "",
                                                DirectoryINode = PKGPFSImageRootDirectory.Attribute("inode")?.Value ?? "",
                                                DirectoryName = PKGPFSImageRootDirectory.Attribute("name")?.Value ?? ""
                                            };

                                            PFSImageRootDirectories.Add(NewPS5PKGRootDirectory);
                                        }
                                    }
                                }

                                // Get the files in uroot
                                var PKGPFSImageURootFiles = PKGConfigurationXML.Descendants("pfs-image").FirstOrDefault()?.Descendants("root").FirstOrDefault()?.Descendants("dir").FirstOrDefault()?.Descendants("file");
                                if (PKGPFSImageURootFiles != null)
                                {
                                    foreach (XElement PKGPFSImageURootFile in PKGPFSImageURootFiles)
                                    {
                                        if (PKGPFSImageURootFile != null)
                                        {
                                            PS5PKGRootFile NewPS5PKGURootFile = new()
                                            {
                                                FileSize = PKGPFSImageURootFile.Attribute("size")?.Value ?? "",
                                                FilePlain = PKGPFSImageURootFile.Attribute("plain")?.Value ?? "",
                                                FileCompression = PKGPFSImageURootFile.Attribute("comp")?.Value ?? "",
                                                FileIMode = PKGPFSImageURootFile.Attribute("imode")?.Value ?? "",
                                                FileIndex = PKGPFSImageURootFile.Attribute("index")?.Value ?? "",
                                                FileINode = PKGPFSImageURootFile.Attribute("inode")?.Value ?? "",
                                                FileName = PKGPFSImageURootFile.Attribute("name")?.Value ?? ""
                                            };

                                            PFSImageURootFiles.Add(NewPS5PKGURootFile);
                                        }
                                    }
                                }

                                // Get the PKG nested image info
                                var PKGNestedImage = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault();
                                if (PKGNestedImage != null)
                                {
                                    string PKGNestedImageVersion = PKGNestedImage.Attribute("version")?.Value ?? "";
                                    string PKGNestedImageReadOnly = PKGNestedImage.Attribute("readonly")?.Value ?? "";
                                    string PKGNestedImageOffset = PKGNestedImage.Attribute("offset")?.Value ?? "";
                                }

                                // Get the PKG nested image sblock info
                                var PKGNestedImageSBlock = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault()?.Descendants("sblock").FirstOrDefault();
                                if (PKGNestedImageSBlock != null)
                                {
                                    string PKGPFSImageSBlockSigned = PKGNestedImageSBlock.Attribute("signed")?.Value ?? "";
                                    string PKGPFSImageSBlockEncrypted = PKGNestedImageSBlock.Attribute("encrypted")?.Value ?? "";
                                    string PKGPFSImageSBlockIgnoreCase = PKGNestedImageSBlock.Attribute("ignore-case")?.Value ?? "";
                                    string PKGPFSImageSBlockIndexSize = PKGNestedImageSBlock.Attribute("index-size")?.Value ?? "";
                                    string PKGPFSImageSBlockBlocks = PKGNestedImageSBlock.Attribute("blocks")?.Value ?? "";
                                    string PKGPFSImageSBlockBackups = PKGNestedImageSBlock.Attribute("backups")?.Value ?? "";
                                }
                                var PKGNestedImageSBlockImageSize = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault()?.Descendants("sblock").FirstOrDefault()?.Element("image-size");
                                if (PKGNestedImageSBlockImageSize != null)
                                {
                                    string PKGPFSImageSBlockImageSizeBlockSize = PKGNestedImageSBlockImageSize.Attribute("block-size")?.Value ?? "";
                                    string PKGPFSImageSBlockImageSizeNum = PKGNestedImageSBlockImageSize.Attribute("num")?.Value ?? "";
                                    string PKGPFSImageSBlockImageSizeValue = PKGNestedImageSBlockImageSize.Value;
                                }
                                var PKGNestedImageSBlockSuperInode = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault()?.Descendants("sblock").FirstOrDefault()?.Element("super-inode");
                                if (PKGNestedImageSBlockSuperInode != null)
                                {
                                    string PKGPFSImageSBlockSuperInodeBlocks = PKGNestedImageSBlockSuperInode.Attribute("blocks")?.Value ?? "";
                                    string PKGPFSImageSBlockSuperInodeInodes = PKGNestedImageSBlockSuperInode.Attribute("inodes")?.Value ?? "";
                                    string PKGPFSImageSBlockSuperInodeRoot = PKGNestedImageSBlockSuperInode.Attribute("root")?.Value ?? "";
                                }
                                var PKGNestedImageSBlockInode = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault()?.Descendants("sblock").FirstOrDefault()?.Descendants("super-inode").FirstOrDefault()?.Element("inode");
                                if (PKGNestedImageSBlockInode != null)
                                {
                                    string PKGPFSImageSBlockInodeSize = PKGNestedImageSBlockInode.Attribute("size")?.Value ?? "";
                                    string PKGPFSImageSBlockInodeLinks = PKGNestedImageSBlockInode.Attribute("links")?.Value ?? "";
                                    string PKGPFSImageSBlockInodeMode = PKGNestedImageSBlockInode.Attribute("mode")?.Value ?? "";
                                    string PKGPFSImageSBlockInodeIMode = PKGNestedImageSBlockInode.Attribute("imode")?.Value ?? "";
                                    string PKGPFSImageSBlockInodeIndex = PKGNestedImageSBlockInode.Attribute("index")?.Value ?? "";
                                }

                                // Get the PKG nested image metadata
                                var PKGNestedImageMetadata = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault()?.Descendants("metadata").FirstOrDefault();
                                if (PKGNestedImageMetadata != null)
                                {
                                    string PKGNestedImageMetadataSize = PKGNestedImageMetadata.Attribute("size")?.Value ?? "";
                                    string PKGNestedImageMetadataPlain = PKGNestedImageMetadata.Attribute("plain")?.Value ?? "";
                                    string PKGNestedImageMetadataCompression = PKGNestedImageMetadata.Attribute("comp")?.Value ?? "";
                                    string PKGNestedImageMetadataOffset = PKGNestedImageMetadata.Attribute("offset")?.Value ?? "";
                                    string PKGNestedImageMetadataPOffset = PKGNestedImageMetadata.Attribute("poffset")?.Value ?? "";
                                    string PKGNestedImageMetadataAfid = PKGNestedImageMetadata.Attribute("afid")?.Value ?? "";
                                }

                                // Get the PKG nested image root info
                                var PKGNestedImageRoot = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault()?.Element("root");
                                if (PKGNestedImageRoot != null)
                                {
                                    string PKGPFSImageRootSize = PKGNestedImageRoot.Attribute("size")?.Value ?? "";
                                    string PKGPFSImageRootLinks = PKGNestedImageRoot.Attribute("links")?.Value ?? "";
                                    string PKGPFSImageRootIMode = PKGNestedImageRoot.Attribute("imode")?.Value ?? "";
                                    string PKGPFSImageRootIndex = PKGNestedImageRoot.Attribute("index")?.Value ?? "";
                                    string PKGPFSImageRootINode = PKGNestedImageRoot.Attribute("inode")?.Value ?? "";
                                    string PKGPFSImageRootName = PKGNestedImageRoot.Attribute("name")?.Value ?? "";
                                }

                                // Get the files in root
                                var PKGNestedImageRootFiles = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault()?.Descendants("root").FirstOrDefault()?.Descendants("file");
                                if (PKGNestedImageRootFiles != null)
                                {
                                    ShowPKGNestedImageFilesButton.IsEnabled = true;

                                    foreach (XElement PKGNestedImageRootFile in PKGNestedImageRootFiles)
                                    {
                                        PS5PKGRootFile NewPS5PKGRootFile = new() { FileSize = PKGNestedImageRootFile.Attribute("size")?.Value ?? "", FilePlain = PKGNestedImageRootFile.Attribute("plain")?.Value ?? "", FileCompression = PKGNestedImageRootFile.Attribute("comp")?.Value ?? "", FileIMode = PKGNestedImageRootFile.Attribute("imode")?.Value ?? "", FileIndex = PKGNestedImageRootFile.Attribute("index")?.Value ?? "", FileINode = PKGNestedImageRootFile.Attribute("inode")?.Value ?? "", FileName = PKGNestedImageRootFile.Attribute("name")?.Value ?? "" };
                                        NestedImageRootFiles.Add(NewPS5PKGRootFile);
                                    }
                                }

                                // Get the directories in root
                                var PKGNestedImageRootDirectories = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault()?.Descendants("root").FirstOrDefault()?.Descendants("dir");
                                if (PKGNestedImageRootDirectories != null)
                                {
                                    ShowPKGNestedImageDirectoriesButton.IsEnabled = true;

                                    foreach (XElement PKGNestedImageRootDirectory in PKGNestedImageRootDirectories)
                                    {
                                        PS5PKGRootDirectory NewPS5PKGRootDirectory = new() { DirectorySize = PKGNestedImageRootDirectory.Attribute("size")?.Value ?? "", DirectoryLinks = PKGNestedImageRootDirectory.Attribute("links")?.Value ?? "", DirectoryIMode = PKGNestedImageRootDirectory.Attribute("imode")?.Value ?? "", DirectoryIndex = PKGNestedImageRootDirectory.Attribute("index")?.Value ?? "", DirectoryINode = PKGNestedImageRootDirectory.Attribute("inode")?.Value ?? "", DirectoryName = PKGNestedImageRootDirectory.Attribute("name")?.Value ?? "" };
                                        NestedImageRootDirectories.Add(NewPS5PKGRootDirectory);
                                    }
                                }

                                // Get the files in uroot
                                var PKGNestedImageURootFiles = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault()?.Descendants("root").FirstOrDefault()?.Descendants("dir").FirstOrDefault()?.Descendants("file");
                                if (PKGNestedImageURootFiles != null)
                                {
                                    foreach (XElement PKGNestedImageURootFile in PKGNestedImageURootFiles)
                                    {
                                        PS5PKGRootFile NewPS5PKGURootFile = new() { FileSize = PKGNestedImageURootFile.Attribute("size")?.Value ?? "", FilePlain = PKGNestedImageURootFile.Attribute("plain")?.Value ?? "", FileCompression = PKGNestedImageURootFile.Attribute("comp")?.Value ?? "", FileIMode = PKGNestedImageURootFile.Attribute("imode")?.Value ?? "", FileIndex = PKGNestedImageURootFile.Attribute("index")?.Value ?? "", FileINode = PKGNestedImageURootFile.Attribute("inode")?.Value ?? "", FileName = PKGNestedImageURootFile.Attribute("name")?.Value ?? "" };
                                        NestedImageURootFiles.Add(NewPS5PKGURootFile);
                                    }
                                }

                                // Get the directories in uroot
                                var PKGNestedImageURootDirectories = PKGConfigurationXML.Descendants("nested-image").FirstOrDefault()?.Descendants("root").FirstOrDefault()?.Descendants("dir").FirstOrDefault()?.Descendants("dir");
                                if (PKGNestedImageURootDirectories != null)
                                {
                                    foreach (XElement PKGNestedImageURootDirectory in PKGNestedImageURootDirectories)
                                    {
                                        PS5PKGRootDirectory NewPS5PKGRootDirectory = new() { DirectorySize = PKGNestedImageURootDirectory.Attribute("size")?.Value ?? "", DirectoryLinks = PKGNestedImageURootDirectory.Attribute("links")?.Value ?? "", DirectoryIMode = PKGNestedImageURootDirectory.Attribute("imode")?.Value ?? "", DirectoryIndex = PKGNestedImageURootDirectory.Attribute("index")?.Value ?? "", DirectoryINode = PKGNestedImageURootDirectory.Attribute("inode")?.Value ?? "", DirectoryName = PKGNestedImageURootDirectory.Attribute("name")?.Value ?? "" };
                                        NestedImageRootDirectories.Add(NewPS5PKGRootDirectory);
                                    }
                                }

                                // Extract param.json & icon0.png
                                using var NewFileStream = new FileStream(selectedFile[0], FileMode.Open, FileAccess.Read);
                                NewFileStream.Seek(0L, SeekOrigin.End);

                                long ContainerOffsetDecValue = 0L;
                                long EntryOffsetDecValue = 0L;
                                int EntrySizeDecValue = 0;

                                long ParamJSONOffsetPosition = 0L;
                                long Icon0OffsetPosition = 0L;
                                long Pic0OffsetPosition = 0L;
                                long Snd0OffsetPosition = 0;

                                long PKGFileLength = NewFileStream.Length;

                                var ParamJsonPKGEntry = new PS5PKGEntry();
                                var Icon0PKGEntry = new PS5PKGEntry();
                                var Pic0PKGEntry = new PS5PKGEntry();
                                var Snd0PKGEntry = new PS5PKGEntry();

                                // Get the param.json and icon0.png PKG entry info
                                foreach (XElement PKGEntry in PKGConfigurationXML.Descendants("entries").Descendants("entry"))
                                {
                                    if (PKGEntry != null)
                                    {
                                        if (PKGEntry.Attribute("name")?.Value == "param.json")
                                        {
                                            ParamJsonPKGEntry.EntryOffset = PKGEntry.Attribute("offset")?.Value ?? "";
                                            ParamJsonPKGEntry.EntrySize = PKGEntry.Attribute("size")?.Value ?? "";
                                            ParamJsonPKGEntry.EntryName = PKGEntry.Attribute("name")?.Value ?? "";
                                        }
                                        if (PKGEntry.Attribute("name")?.Value == "icon0.png")
                                        {
                                            Icon0PKGEntry.EntryOffset = PKGEntry.Attribute("offset")?.Value ?? "";
                                            Icon0PKGEntry.EntrySize = PKGEntry.Attribute("size")?.Value ?? "";
                                            Icon0PKGEntry.EntryName = PKGEntry.Attribute("name")?.Value ?? "";
                                        }
                                        if (PKGEntry.Attribute("name")?.Value == "pic0.png")
                                        {
                                            Pic0PKGEntry.EntryOffset = PKGEntry.Attribute("offset")?.Value ?? "";
                                            Pic0PKGEntry.EntrySize = PKGEntry.Attribute("size")?.Value ?? "";
                                            Pic0PKGEntry.EntryName = PKGEntry.Attribute("name")?.Value ?? "";
                                        }
                                        if (PKGEntry.Attribute("name")?.Value == "snd0.at9")
                                        {
                                            Snd0PKGEntry.EntryOffset = PKGEntry.Attribute("offset")?.Value ?? "";
                                            Snd0PKGEntry.EntrySize = PKGEntry.Attribute("size")?.Value ?? "";
                                            Snd0PKGEntry.EntryName = PKGEntry.Attribute("name")?.Value ?? "";
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
                                    NewFileStream.Seek(ParamJSONOffsetPosition, SeekOrigin.Begin);
                                    NewFileStream.ReadExactly(ParamFileBuffer);

                                    if (!string.IsNullOrWhiteSpace(Encoding.UTF8.GetString(ParamFileBuffer)))
                                    {
                                        CurrentParamJSON = Encoding.UTF8.GetString(ParamFileBuffer);
                                        var ParamData = JsonConvert.DeserializeObject<PS5Param>(Encoding.UTF8.GetString(ParamFileBuffer));
                                        var NewPS5Game = new PS5Game();

                                        if (ParamData != null)
                                        {
                                            ExportParamJSONButton.IsEnabled = true;

                                            if (ParamData.TitleId != null)
                                            {
                                                NewPS5Game.GameID = "Title ID: " + ParamData.TitleId;
                                                NewPS5Game.GameRegion = "Region: " + PS5Game.GetGameRegion(ParamData.TitleId);
                                            }

                                            bool NonENUSTitle = false;
                                            if (ParamData.LocalizedParameters!.EnUS is not null)
                                            {
                                                NewPS5Game.GameTitle = ParamData.LocalizedParameters.EnUS.TitleName;
                                            }
                                            else
                                            {
                                                NonENUSTitle = true;
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
                                            if (NonENUSTitle)
                                            {
                                                NewPS5Game.GameTitle = GetAlternativeGameTitle(ParamData);
                                            }

                                            if (ParamData.ContentId != null)
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

                                            long PS5GameSize = PKGFileLength;
                                            NewPS5Game.GameSize = $"Size: {HumanReadableBytes(PS5GameSize)}";

                                            if (ParamData.ContentVersion != null)
                                            {
                                                NewPS5Game.GameVersion = "Version: " + ParamData.ContentVersion;
                                            }
                                            if (ParamData.RequiredSystemSoftwareVersion != null)
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
                                    NewFileStream.Seek(Icon0OffsetPosition, SeekOrigin.Begin);
                                    NewFileStream.ReadExactly(Icon0FileBuffer);

                                    // Check the buffer and display the icon
                                    if (Icon0FileBuffer != null)
                                    {
                                        AnyBitmap Icon0BitmapImage = AnyBitmap.FromBytes(Icon0FileBuffer);
                                        PKGIconImage.Source = AnyBitmapToIImage(Icon0BitmapImage);
                                        CurrentIcon0 = Icon0BitmapImage;
                                        ExportIcon0PNGButton.IsEnabled = true;
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
                                    NewFileStream.Seek(Pic0OffsetPosition, SeekOrigin.Begin);
                                    NewFileStream.ReadExactly(Pic0FileBuffer);

                                    // Check the buffer and display the icon
                                    if (Pic0FileBuffer != null)
                                    {
                                        AnyBitmap Pic0BitmapImage = AnyBitmap.FromBytes(Pic0FileBuffer);
                                        PKGIconImage.Source = AnyBitmapToIImage(Pic0BitmapImage);
                                        CurrentPic0 = Pic0BitmapImage;
                                        ExportPic0Button.IsEnabled = true;
                                    }
                                }

                                // SND0.AT9
                                if (!string.IsNullOrEmpty(Snd0PKGEntry.EntryOffset) && !string.IsNullOrEmpty(Snd0PKGEntry.EntrySize))
                                {
                                    if (!string.IsNullOrEmpty(PKGMountImageContainerOffset))
                                    {
                                        ContainerOffsetDecValue = Convert.ToInt64(PKGMountImageContainerOffset, 16);
                                    }
                                    if (!string.IsNullOrEmpty(Snd0PKGEntry.EntryOffset))
                                    {
                                        EntryOffsetDecValue = Convert.ToInt64(Snd0PKGEntry.EntryOffset, 16);
                                    }
                                    if (!string.IsNullOrEmpty(Snd0PKGEntry.EntrySize))
                                    {
                                        EntrySizeDecValue = Convert.ToInt32(Snd0PKGEntry.EntrySize, 16);
                                    }
                                    Snd0OffsetPosition = ContainerOffsetDecValue + EntryOffsetDecValue;

                                    byte[] Snd0FileBuffer = new byte[EntrySizeDecValue];
                                    NewFileStream.Seek(Snd0OffsetPosition, SeekOrigin.Begin);
                                    NewFileStream.ReadExactly(Snd0FileBuffer);

                                    if (Snd0FileBuffer != null)
                                    {
                                        ExportSnd0Button.IsEnabled = true;
                                        CurrentSnd0 = Snd0FileBuffer;
                                    }
                                }

                                NewFileStream.Close();
                            }
                        }
                    }
                    else { return; }
                }
            }
        }

        private void ShowPKGPFSImageFilesButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentListViewTitleTextBlock.Text = "PKG PFS Image Files :";

            HideListBoxes();
            PKGImageFilesListBox.IsVisible = true;
            PKGImageFilesListBox.Items.Clear();

            if (PFSImageRootFiles != null && PFSImageRootFiles.Count > 0)
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

            if (PFSImageRootDirectories != null && PFSImageRootDirectories.Count > 0)
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

            if (NestedImageRootFiles != null && NestedImageRootFiles.Count > 0)
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

            if (NestedImageRootDirectories != null && NestedImageRootDirectories.Count > 0)
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
            if (CurrentConfigurationXML != null)
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
            if (CurrentIcon0 != null)
            {
                if (VisualRoot is not Window window)
                    return;

                var newSaveFileDialog = new SaveFileDialog() { Title = "Select a save path", Filters = [new FileDialogFilter() { Name = "PNG files", Extensions = { "png" } }] };
                var selectedSaveFile = await newSaveFileDialog.ShowAsync(window);
                if (selectedSaveFile != null)
                {
                    CurrentIcon0.SaveAs(selectedSaveFile);
                }
            }
        }

        private async void ExportPic0Button_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentPic0 != null)
            {
                if (VisualRoot is not Window window)
                    return;

                var newSaveFileDialog = new SaveFileDialog() { Title = "Select a save path", Filters = [new FileDialogFilter() { Name = "PNG files", Extensions = { "png" } }] };
                var selectedSaveFile = await newSaveFileDialog.ShowAsync(window);
                if (selectedSaveFile != null)
                {
                    CurrentPic0.SaveAs(selectedSaveFile);
                }
            }
        }

        private async void ExportSnd0Button_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSnd0 != null)
            {
                if (VisualRoot is not Window window)
                    return;

                var newSaveFileDialog = new SaveFileDialog() { Title = "Select a save path", Filters = [new FileDialogFilter() { Name = "AT9 files", Extensions = { "at9" } }] };
                var selectedSaveFile = await newSaveFileDialog.ShowAsync(window);
                if (selectedSaveFile != null)
                {
                    await File.WriteAllBytesAsync(selectedSaveFile, CurrentSnd0);
                }
            }
        }

        #region Structures & Functions

        public struct PS5PKGEntry
        {
            public string EntryOffset { get; set; }

            public string EntrySize { get; set; }

            public string EntryName { get; set; }
        }

        public struct PS5PKGScenario
        {
            public string ScenarioID { get; set; }

            public string ScenarioType { get; set; }

            public string ScenarioName { get; set; }
        }

        public struct PS5PKGChunk
        {
            public string ChunkID { get; set; }

            public string ChunkFlag { get; set; }

            public string ChunkLocus { get; set; }

            public string ChunkLanguage { get; set; }

            public string ChunkDisps { get; set; }

            public string ChunkNum { get; set; }

            public string ChunkSize { get; set; }

            public string ChunkName { get; set; }

            public string ChunkValue { get; set; }
        }

        public struct PS5PKGOuter
        {
            public string OuterID { get; set; }

            public string OuterImage { get; set; }

            public string OuterOffset { get; set; }

            public string OuterSize { get; set; }

            public string OuterChunks { get; set; }
        }

        public struct PS5PKGRootDirectory
        {
            public string DirectorySize { get; set; }

            public string DirectoryLinks { get; set; }

            public string DirectoryIMode { get; set; }

            public string DirectoryIndex { get; set; }

            public string DirectoryINode { get; set; }

            public string DirectoryName { get; set; }
        }

        public struct PS5PKGRootFile
        {
            public string FileSize { get; set; }

            public string FilePlain { get; set; }

            public string FileCompression { get; set; }

            public string FileIMode { get; set; }

            public string FileIndex { get; set; }

            public string FileINode { get; set; }

            public string FileName { get; set; }
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

        private static string? GetAlternativeGameTitle(PS5Param ParamFile)
        {
            var Languages = ParamFile.LocalizedParameters;
            if (Languages == null) return null;

            if (Languages.EnGB != null) return Languages.EnGB.TitleName;
            if (Languages.JaJP != null) return Languages.JaJP.TitleName;
            if (Languages.KoKR != null) return Languages.KoKR.TitleName;
            if (Languages.ZhHant != null) return Languages.ZhHant.TitleName;
            if (Languages.ZhHans != null) return Languages.ZhHans.TitleName;
            if (Languages.ArAE != null) return Languages.ArAE.TitleName;

            return null;
        }

        public static IImage AnyBitmapToIImage(AnyBitmap initialAnyBitmap)
        {
            IImage NewImage = new Bitmap(initialAnyBitmap.GetStream());
            return NewImage;
        }

        public static string HumanReadableBytes(long bytes, bool useBinary = true)
        {
            double baseUnit = useBinary ? 1024.0 : 1000.0;
            string[] units = useBinary ? ["B", "KiB", "MiB", "GiB", "TiB"] : ["B", "kB", "MB", "GB", "TB"];

            if (bytes < 0) return "-" + HumanReadableBytes(-bytes, useBinary);
            if (bytes == 0) return "0 B";

            int exp = (int)(Math.Log(bytes) / Math.Log(baseUnit));
            exp = Math.Min(exp, units.Length - 1);
            double value = bytes / Math.Pow(baseUnit, exp);
            return $"{value:N2} {units[exp]}";
        }

        #endregion

    }

#pragma warning restore CS0618 // Type or member is obsolete

}