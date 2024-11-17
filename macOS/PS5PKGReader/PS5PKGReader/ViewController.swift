//
//  ViewController.swift
//  PS5PKGReader
//
//  Created by SvenGDK on 16/11/2024.
//

import Cocoa

class ViewController: NSViewController, NSTableViewDataSource, NSTableViewDelegate {

    var PKGEntries: [PS5PKGEntry] = []
    var PKGChunks: [PS5PKGChunk] = []
    var PKGScenarios: [PS5PKGScenario] = []
    var PKGOuters: [PS5PKGOuter] = []
    
    var PFSImageRootFiles: [PS5PKGRootFile] = []
    var PFSImageRootDirectories: [PS5PKGRootDirectory] = []
    var PFSImageURootFiles: [PS5PKGRootFile] = []
    
    var NestedImageRootFiles: [PS5PKGRootFile] = []
    var NestedImageRootDirectories: [PS5PKGRootDirectory] = []
    var NestedImageURootFiles: [PS5PKGRootFile] = []
    var NestedImageURootDirectories: [PS5PKGRootDirectory] = []
    
    var CurrentFileList: [PS5PKGRootFile] = []
    var CurrentDirectoryList: [PS5PKGRootDirectory] = []

    var IsSourcePKG: Bool = false
    var IsRetailPKG: Bool = false

    var CurrentParamJSON: String? = nil
    var CurrentConfigurationXML: XMLDocument? = nil
    var CurrentIcon0: NSImage? = nil
    var CurrentPic0: NSImage? = nil
    
    @IBOutlet weak var CurrentListTextField: NSTextField!
    @IBOutlet weak var SelectedPKGTextField: NSTextField!
    @IBOutlet weak var GameTitleTextField: NSTextField!
    @IBOutlet weak var GameIDTextField: NSTextField!
    @IBOutlet weak var GameRegionTextField: NSTextField!
    @IBOutlet weak var GameVersionTextField: NSTextField!
    @IBOutlet weak var GameContentIDTextField: NSTextField!
    @IBOutlet weak var GameCategoryTextField: NSTextField!
    @IBOutlet weak var GameSizeTextField: NSTextField!
    @IBOutlet weak var GameRequiredFirmwareTextField: NSTextField!
    @IBOutlet weak var GameIconImageView: NSImageView!
    @IBOutlet weak var PKGEntriesTableView: NSTableView!
    @IBOutlet weak var PKGEntriesScrollView: NSScrollView!
    @IBOutlet weak var PKGScenariosTableView: NSTableView!
    @IBOutlet weak var PKGScenariosScrollView: NSScrollView!
    @IBOutlet weak var PKGChunksTableView: NSTableView!
    @IBOutlet weak var PKGChunksScrollView: NSScrollView!
    @IBOutlet weak var PKGOutersTableView: NSTableView!
    @IBOutlet weak var PKGOutersScrollView: NSScrollView!
    @IBOutlet weak var PKGImageFilesTableView: NSTableView!
    @IBOutlet weak var PKGImageFilesScrollView: NSScrollView!
    @IBOutlet weak var PKGImageDirectoriesTableView: NSTableView!
    @IBOutlet weak var PKGImageDirectoriesScrollView: NSScrollView!
    
    
    override func viewDidLoad() {
        super.viewDidLoad()

    }

    override var representedObject: Any? {
        didSet {
        }
    }
    
    @IBAction func BrowsePKGFile(_ sender: NSButton) {
        
        let openPanel = NSOpenPanel()
        openPanel.allowedFileTypes = ["pkg"]
        openPanel.allowsMultipleSelection = false

        if openPanel.runModal() == .OK, let pkgURL = openPanel.url {
            
            SelectedPKGTextField.stringValue = pkgURL.path
            
            // Clear lists
            PKGEntries.removeAll()
            PKGScenarios.removeAll()
            PKGOuters.removeAll()
            PKGChunks.removeAll()
            
            PFSImageRootFiles.removeAll()
            PFSImageRootDirectories.removeAll()
            PFSImageURootFiles.removeAll()
            NestedImageRootFiles.removeAll()
            NestedImageRootDirectories.removeAll()
            NestedImageURootFiles.removeAll()
            NestedImageURootDirectories.removeAll()
            
            CurrentFileList.removeAll()
            CurrentDirectoryList.removeAll()

            // Reset
            GameIconImageView.image = nil
            IsSourcePKG = false
            IsRetailPKG = false
            CurrentParamJSON = ""
            CurrentConfigurationXML = nil
            CurrentIcon0 = nil
            CurrentPic0 = nil
            
            var firstString = ""
            var int8AtOffset5: Int8 = 0
            
            do {
                let fileHandle = try FileHandle(forReadingFrom: pkgURL)
                defer { fileHandle.closeFile() }
                
                let data = fileHandle.readData(ofLength: 4)
                let str = String(data: data, encoding: .ascii)
                firstString = str!
                
                try? fileHandle.seek(toOffset: 5)
                let seconddata = fileHandle.readData(ofLength: 1)
                int8AtOffset5 = Int8(bitPattern: seconddata[0])
                
            } catch {
                return
            }

            // Determine PS5 PKG
            if !firstString.isEmpty {
                IsSourcePKG = firstString.contains("CNT")
            }
            if int8AtOffset5 == -128 {
                IsRetailPKG = true
            }
            
            if IsRetailPKG || IsSourcePKG {

                // Get param.json start & end offset
                let ParamJSONOffsets = FindOffsetsInFile(filePath: pkgURL.path, startString: "param.json", endString: "version.xml")
                if ParamJSONOffsets.count > 0 {
                
                    // Extract param.json
                    let ExtractedParam = ExtractData(FilePath: pkgURL.path, StartOffset: ParamJSONOffsets[0], EndOffset: ParamJSONOffsets[1], FileName: "param.json")
                    if ExtractedParam!.count > 0 {
                        
                        CurrentParamJSON = ExtractedParam!
                        
                        // Decode param.json and show values
                        do {
                            let PS5ParamData = try JSONDecoder().decode(PS5Param.self, from: ExtractedParam!.data(using: .utf8)!)
                            let NewPS5GameOrApp: PS5GameOrApp = PS5GameOrApp(GameAppPath: pkgURL.path,
                                                                             GameAppTitle: PS5ParamData.localizedParameters!.enUS!.titleName,
                                                                             GameAppID: PS5ParamData.titleID,
                                                                             GameAppContentID: PS5ParamData.contentID,
                                                                             GameAppVersion: PS5ParamData.contentVersion,
                                                                             GameAppRequiredFirmware: PS5ParamData.requiredSystemSoftwareVersion)
                            
                            GameTitleTextField.stringValue = NewPS5GameOrApp.GameAppTitle!
                            GameIDTextField.stringValue = "ID: " + NewPS5GameOrApp.GameAppID!
                            GameContentIDTextField.stringValue = "Content ID: " + NewPS5GameOrApp.GameAppContentID!
                            GameVersionTextField.stringValue = "Version: " + NewPS5GameOrApp.GameAppVersion!
                            GameRegionTextField.stringValue = GetRegion(ID: NewPS5GameOrApp.GameAppID!)
                            
                            var AdjustedReqFw = NewPS5GameOrApp.GameAppRequiredFirmware!.replacingOccurrences(of: "0x", with: "")
                            AdjustedReqFw.insert(".", at: AdjustedReqFw.index(AdjustedReqFw.startIndex, offsetBy: 2))
                            AdjustedReqFw.insert(".", at: AdjustedReqFw.index(AdjustedReqFw.startIndex, offsetBy: 5))
                            AdjustedReqFw.insert(".", at: AdjustedReqFw.index(AdjustedReqFw.startIndex, offsetBy: 8))
                            GameRequiredFirmwareTextField.stringValue = "Required Firmware: " + AdjustedReqFw
                            
                            let fileAttributes = try? FileManager.default.attributesOfItem(atPath: pkgURL.path)
                            if let fileSize = fileAttributes?[.size] as? NSNumber {
                                let gameSize = String(format: "Size: %.2f GB", fileSize.doubleValue / 1073741824)
                                GameSizeTextField.stringValue = gameSize
                            }
                            
                            // Determine category
                            switch PS5ParamData.applicationCategoryType {
                            case 0:
                                GameCategoryTextField.stringValue = "Category: Game"
                            case 65792:
                                GameCategoryTextField.stringValue = "Category: RNPS Media App"
                            case 131328:
                                GameCategoryTextField.stringValue = "Category: System Built-in App"
                            case 131584:
                                GameCategoryTextField.stringValue = "Category: Big Daemon"
                            case 16777216:
                                GameCategoryTextField.stringValue = "Category: ShellUI"
                            case 33554432:
                                GameCategoryTextField.stringValue = "Category: Daemon"
                            case 67108864:
                                GameCategoryTextField.stringValue = "Category: ShellApp"
                            default:
                                GameCategoryTextField.stringValue = "Category: Unknown"
                            }
                        } catch {
                            return
                        }
                        
                    }
                    
                }
                
                
            } else {
                     
                // Probably a self created PKG that contains a package configuration
                let PackageConfigurationOffsets = FindOffsetsInFile(filePath: pkgURL.path, startString: "<package-configuration version=\"1.0\" type=\"package-info\">", endString: "</package-configuration>")
                if PackageConfigurationOffsets.count > 0 {
                    
                    let ExtractedPackageConfiguration = ExtractData(FilePath: pkgURL.path, StartOffset: PackageConfigurationOffsets[0], EndOffset: PackageConfigurationOffsets[1], FileName: "package-configuration.xml")
                    if ExtractedPackageConfiguration!.count > 0 {
                        
                        // Load the XML file
                        let PKGConfigurationXML = try! XMLDocument(xmlString: ExtractedPackageConfiguration!, options: [])
                        CurrentConfigurationXML = PKGConfigurationXML

                        // Get the PKG config values
                        if let pkgConfig = PKGConfigurationXML.rootElement()?.elements(forName: "config").first {
                            let pkgConfigVersion = pkgConfig.attribute(forName: "version")?.stringValue
                            let pkgConfigMetadata = pkgConfig.attribute(forName: "metadata")?.stringValue
                            let pkgConfigPrimary = pkgConfig.attribute(forName: "primary")?.stringValue
                        }

                        let pkgConfigContentID = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "content-id").first?.stringValue ?? ""
                        let pkgConfigPrimaryID = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "primary-id").first?.stringValue ?? ""
                        let pkgConfigLongName = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "longname").first?.stringValue ?? ""
                        let pkgConfigRequiredSystemVersion = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "required-system-version").first?.stringValue ?? ""
                        let pkgConfigDRMType = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "drm-type").first?.stringValue ?? ""
                        let pkgConfigContentType = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "content-type").first?.stringValue ?? ""
                        let pkgConfigApplicationType = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "application-type").first?.stringValue ?? ""
                        let pkgConfigNumberOfImages = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "num-of-images").first?.stringValue ?? ""
                        let pkgConfigSize = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "package-size").first?.stringValue ?? ""
                        let pkgConfigVersionDate = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "version-date").first?.stringValue ?? ""
                        let pkgConfigVersionHash = PKGConfigurationXML.rootElement()?.elements(forName: "config").first?.elements(forName: "version-hash").first?.stringValue ?? ""

                        // Get the PKG digests
                        if let pkgDigests = PKGConfigurationXML.rootElement()?.elements(forName: "digests").first {
                            let pkgDigestsVersion = pkgDigests.attribute(forName: "version")?.stringValue
                            let pkgDigestsMajorParamVersion = pkgDigests.attribute(forName: "major-param-version")?.stringValue
                        }
                           
                        let pkgContentDigest = PKGConfigurationXML.rootElement()?.elements(forName: "digests").first?.elements(forName: "content-digest").first?.stringValue ?? ""
                        let pkgGameDigest = PKGConfigurationXML.rootElement()?.elements(forName: "digests").first?.elements(forName: "game-digest").first?.stringValue ?? ""
                        let pkgHeaderDigest = PKGConfigurationXML.rootElement()?.elements(forName: "digests").first?.elements(forName: "header-digest").first?.stringValue ?? ""
                        let pkgSystemDigest = PKGConfigurationXML.rootElement()?.elements(forName: "digests").first?.elements(forName: "system-digest").first?.stringValue ?? ""
                        let pkgParamDigest = PKGConfigurationXML.rootElement()?.elements(forName: "digests").first?.elements(forName: "param-digest").first?.stringValue ?? ""
                        let pkgDigest = PKGConfigurationXML.rootElement()?.elements(forName: "digests").first?.elements(forName: "package-digest").first?.stringValue ?? ""

                        // Get the PKG params
                        let pkgParams = PKGConfigurationXML.rootElement()?.elements(forName: "params").first
                        let pkgParamApplicationDRMType = pkgParams?.elements(forName: "applicationDrmType").first?.stringValue
                        let pkgParamContentID = pkgParams?.elements(forName: "contentId").first?.stringValue
                        let pkgParamContentVersion = pkgParams?.elements(forName: "contentVersion").first?.stringValue
                        let pkgParamMasterVersion = pkgParams?.elements(forName: "masterVersion").first?.stringValue
                        let pkgParamRequiredSystemVersion = pkgParams?.elements(forName: "requiredSystemSoftwareVersion").first?.stringValue
                        let pkgParamSDKVersion = pkgParams?.elements(forName: "sdkVersion").first?.stringValue
                        let pkgParamTitleName = pkgParams?.elements(forName: "titleName").first?.stringValue

                        // Get the PKG container information
                        let pkgContainer = PKGConfigurationXML.rootElement()?.elements(forName: "container").first
                        let pkgContainerSize = pkgContainer?.elements(forName: "container-size").first?.stringValue
                        let pkgContainerMandatorySize = pkgContainer?.elements(forName: "mandatory-size").first?.stringValue
                        let pkgContainerBodyOffset = pkgContainer?.elements(forName: "body-offset").first?.stringValue
                        let pkgContainerBodySize = pkgContainer?.elements(forName: "body-size").first?.stringValue
                        let pkgContainerBodyDigest = pkgContainer?.elements(forName: "body-digest").first?.stringValue
                        let pkgContainerPromoteSize = pkgContainer?.elements(forName: "promote-size").first?.stringValue

                        // Get the PKG mount image
                        let pkgMountImage = PKGConfigurationXML.rootElement()?.elements(forName: "mount-image").first
                        let pkgMountImagePFSOffsetAlign = pkgMountImage?.elements(forName: "pfs-offset-align").first?.stringValue
                        let pkgMountImagePFSSizeAlign = pkgMountImage?.elements(forName: "pfs-size-align").first?.stringValue
                        let pkgMountImagePFSImageOffset = pkgMountImage?.elements(forName: "pfs-image-offset").first?.stringValue
                        let pkgMountImagePFSImageSize = pkgMountImage?.elements(forName: "pfs-image-size").first?.stringValue
                        let pkgMountImageFixedInfoSize = pkgMountImage?.elements(forName: "fixed-info-size").first?.stringValue
                        let pkgMountImagePFSImageSeed = pkgMountImage?.elements(forName: "pfs-image-seed").first?.stringValue
                        let pkgMountImageSBlockDigest = pkgMountImage?.elements(forName: "sblock-digest").first?.stringValue
                        let pkgMountImageFixedInfoDigest = pkgMountImage?.elements(forName: "fixed-info-digest").first?.stringValue
                        let pkgMountImageOffset = pkgMountImage?.elements(forName: "mount-image-offset").first?.stringValue
                        let pkgMountImageSize = pkgMountImage?.elements(forName: "mount-image-size").first?.stringValue
                        let pkgMountImageContainerOffset = pkgMountImage?.elements(forName: "container-offset").first?.stringValue
                        let pkgMountImageSupplementalOffset = pkgMountImage?.elements(forName: "supplemental-offset").first?.stringValue
                        
                        // Get the PKG entries and add to pkgContentListView
                        let pkgEntries = PKGConfigurationXML.rootElement()?.elements(forName: "entries").first?.elements(forName: "entry") ?? []
                        for pkgEntry in pkgEntries {
                            var newPS5PKGEntry = PS5PKGEntry()
                            newPS5PKGEntry.EntryOffset = pkgEntry.attribute(forName: "offset")?.stringValue ?? ""
                            newPS5PKGEntry.EntrySize = pkgEntry.attribute(forName: "size")?.stringValue ?? ""
                            newPS5PKGEntry.EntryName = pkgEntry.attribute(forName: "name")?.stringValue ?? ""
                            PKGEntries.append(newPS5PKGEntry)
                        }
                        PKGEntriesTableView.reloadData()

                        // Get the PKG chunkinfo
                        if let pkgChunkInfo = PKGConfigurationXML.rootElement()?.elements(forName: "chunkinfo").first {
                            let pkgChunkInfoSize = pkgChunkInfo.attribute(forName: "size")?.stringValue
                            let pkgChunkInfoNested = pkgChunkInfo.attribute(forName: "nested")?.stringValue
                            let pkgChunkInfoSDK = pkgChunkInfo.attribute(forName: "sdk")?.stringValue
                            let pkgChunkInfoDisps = pkgChunkInfo.attribute(forName: "disps")?.stringValue
                        }
                        let pkgChunkInfoContentID = PKGConfigurationXML.rootElement()?.elements(forName: "chunkinfo").first?.elements(forName: "contentid").first?.stringValue ?? ""
                        let pkgChunkInfoLanguages = PKGConfigurationXML.rootElement()?.elements(forName: "chunkinfo").first?.elements(forName: "languages").first?.stringValue ?? ""

                        // Get the PKG chunkinfo scenarios
                        let pkgChunkInfoScenarios = PKGConfigurationXML.rootElement()?.elements(forName: "chunkinfo").first?.elements(forName: "scenarios").first?.elements(forName: "scenario") ?? []
                        for pkgChunkInfoScenario in pkgChunkInfoScenarios {
                            var newPS5PKGChunkInfoScenario = PS5PKGScenario()
                            newPS5PKGChunkInfoScenario.ScenarioID = pkgChunkInfoScenario.attribute(forName: "id")?.stringValue ?? ""
                            newPS5PKGChunkInfoScenario.ScenarioType = pkgChunkInfoScenario.attribute(forName: "type")?.stringValue ?? ""
                            newPS5PKGChunkInfoScenario.ScenarioName = pkgChunkInfoScenario.attribute(forName: "name")?.stringValue ?? ""
                            PKGScenarios.append(newPS5PKGChunkInfoScenario)
                        }
                        PKGScenariosTableView.reloadData()

                        // Get the PKG chunkinfo chunks
                        if let pkgChunkInfoChunks = PKGConfigurationXML.rootElement()?.elements(forName: "chunks").first {
                            let pkgChunkInfoChunksNum = pkgChunkInfoChunks.attribute(forName: "num")?.stringValue
                            let pkgChunkInfoChunksDefault = pkgChunkInfoChunks.attribute(forName: "default")?.stringValue
                        }
                        let pkgChunkInfoChunksList = PKGConfigurationXML.rootElement()?.elements(forName: "chunkinfo").first?.elements(forName: "chunks").first?.elements(forName: "chunk") ?? []
                        for pkgChunkInfoChunk in pkgChunkInfoChunksList {
                            var newPS5PKGChunkInfoChunk = PS5PKGChunk()
                            newPS5PKGChunkInfoChunk.ChunkID = pkgChunkInfoChunk.attribute(forName: "id")?.stringValue ?? ""
                            newPS5PKGChunkInfoChunk.ChunkFlag = pkgChunkInfoChunk.attribute(forName: "flag")?.stringValue ?? ""
                            newPS5PKGChunkInfoChunk.ChunkLocus = pkgChunkInfoChunk.attribute(forName: "locus")?.stringValue ?? ""
                            newPS5PKGChunkInfoChunk.ChunkLanguage = pkgChunkInfoChunk.attribute(forName: "language")?.stringValue ?? ""
                            newPS5PKGChunkInfoChunk.ChunkDisps = pkgChunkInfoChunk.attribute(forName: "disps")?.stringValue ?? ""
                            newPS5PKGChunkInfoChunk.ChunkNum = pkgChunkInfoChunk.attribute(forName: "num")?.stringValue ?? ""
                            newPS5PKGChunkInfoChunk.ChunkSize = pkgChunkInfoChunk.attribute(forName: "size")?.stringValue ?? ""
                            newPS5PKGChunkInfoChunk.ChunkName = pkgChunkInfoChunk.attribute(forName: "name")?.stringValue ?? ""
                            newPS5PKGChunkInfoChunk.ChunkValue = pkgChunkInfoChunk.stringValue ?? ""
                            PKGChunks.append(newPS5PKGChunkInfoChunk)
                        }
                        PKGChunksTableView.reloadData()
                        
                        // Get the PKG chunkinfo outers
                        if let pkgChunkInfoOuters = PKGConfigurationXML.rootElement()?.elements(forName: "outers").first {
                            let pkgChunkInfoOutersNum = pkgChunkInfoOuters.attribute(forName: "num")?.stringValue
                            let pkgChunkInfoOutersOverlapped = pkgChunkInfoOuters.attribute(forName: "overlapped")?.stringValue
                            let pkgChunkInfoOutersLanguageOverlapped = pkgChunkInfoOuters.attribute(forName: "language-overlapped")?.stringValue
                        }

                        let pkgChunkInfoOutersList = PKGConfigurationXML.rootElement()?.elements(forName: "chunkinfo").flatMap { $0.elements(forName: "outers") }.flatMap { $0.elements(forName: "outer") }
                        for pkgChunkInfoOuter in pkgChunkInfoOutersList ?? [] {
                            var newPS5PKGOuter = PS5PKGOuter() // Assuming PS5PKGOuter is defined elsewhere
                            newPS5PKGOuter.OuterID = pkgChunkInfoOuter.attribute(forName: "id")?.stringValue ?? ""
                            newPS5PKGOuter.OuterImage = pkgChunkInfoOuter.attribute(forName: "image")?.stringValue ?? ""
                            newPS5PKGOuter.OuterOffset = pkgChunkInfoOuter.attribute(forName: "offset")?.stringValue ?? ""
                            newPS5PKGOuter.OuterSize = pkgChunkInfoOuter.attribute(forName: "size")?.stringValue ?? ""
                            newPS5PKGOuter.OuterChunks = pkgChunkInfoOuter.attribute(forName: "chunks")?.stringValue ?? ""
                            PKGOuters.append(newPS5PKGOuter)
                        }
                        PKGOutersTableView.reloadData()

                        // Get the PKG pfs image info
                        if let pkgPFSImage = PKGConfigurationXML.rootElement()?.elements(forName: "pfs-image").first {
                            let pkgPFSImageVersion = pkgPFSImage.attribute(forName: "version")?.stringValue
                            let pkgPFSImageReadOnly = pkgPFSImage.attribute(forName: "readonly")?.stringValue
                            let pkgPFSImageOffset = pkgPFSImage.attribute(forName: "offset")?.stringValue
                            let pkgPFSImageMetadata = pkgPFSImage.attribute(forName: "metadata")?.stringValue
                        }
                        
                        // Get the PKG pfs image sblock info
                        if let pkgPFSImageSBlock = PKGConfigurationXML.rootElement()?.elements(forName: "sblock").first {
                            let pkgPFSImageSBlockSigned = pkgPFSImageSBlock.attribute(forName: "signed")?.stringValue
                            let pkgPFSImageSBlockEncrypted = pkgPFSImageSBlock.attribute(forName: "encrypted")?.stringValue
                            let pkgPFSImageSBlockIgnoreCase = pkgPFSImageSBlock.attribute(forName: "ignore-case")?.stringValue
                            let pkgPFSImageSBlockIndexSize = pkgPFSImageSBlock.attribute(forName: "index-size")?.stringValue
                            let pkgPFSImageSBlockBlocks = pkgPFSImageSBlock.attribute(forName: "blocks")?.stringValue
                            let pkgPFSImageSBlockBackups = pkgPFSImageSBlock.attribute(forName: "backups")?.stringValue
                        }
                        if let pkgPFSImageSBlockImageSize = PKGConfigurationXML.rootElement()?.elements(forName: "sblock").first?.elements(forName: "image-size").first {
                            let pkgPFSImageSBlockImageSizeBlockSize = pkgPFSImageSBlockImageSize.attribute(forName: "block-size")?.stringValue
                            let pkgPFSImageSBlockImageSizeNum = pkgPFSImageSBlockImageSize.attribute(forName: "num")?.stringValue
                            let pkgPFSImageSBlockImageSizeValue = pkgPFSImageSBlockImageSize.stringValue
                        }
                        if let pkgPFSImageSBlockSuperInode = PKGConfigurationXML.rootElement()?.elements(forName: "sblock").first?.elements(forName: "super-inode").first {
                            let pkgPFSImageSBlockSuperInodeBlocks = pkgPFSImageSBlockSuperInode.attribute(forName: "blocks")?.stringValue
                            let pkgPFSImageSBlockSuperInodeInodes = pkgPFSImageSBlockSuperInode.attribute(forName: "inodes")?.stringValue
                            let pkgPFSImageSBlockSuperInodeRoot = pkgPFSImageSBlockSuperInode.attribute(forName: "root")?.stringValue
                        }
                        if let pkgPFSImageSBlockInode = PKGConfigurationXML.rootElement()?.elements(forName: "sblock").first?.elements(forName: "super-inode").first?.elements(forName: "inode").first {
                            let pkgPFSImageSBlockInodeSize = pkgPFSImageSBlockInode.attribute(forName: "size")?.stringValue
                            let pkgPFSImageSBlockInodeLinks = pkgPFSImageSBlockInode.attribute(forName: "links")?.stringValue
                            let pkgPFSImageSBlockInodeMode = pkgPFSImageSBlockInode.attribute(forName: "mode")?.stringValue
                            let pkgPFSImageSBlockInodeIMode = pkgPFSImageSBlockInode.attribute(forName: "imode")?.stringValue
                            let pkgPFSImageSBlockInodeIndex = pkgPFSImageSBlockInode.attribute(forName: "index")?.stringValue
                        }
                        let pkgPFSImageSBlockSeed = PKGConfigurationXML.rootElement()?.elements(forName: "sblock").first?.elements(forName: "seed").first?.stringValue
                        let pkgPFSImageSBlockICV = PKGConfigurationXML.rootElement()?.elements(forName: "sblock").first?.elements(forName: "icv").first?.stringValue
                        
                        // Get the PKG pfs image root info
                        if let pkgPFSImageRoot = PKGConfigurationXML.rootElement()?.elements(forName: "pfs-image").first?.elements(forName: "root").first {
                            let pkgPFSImageRootSize = pkgPFSImageRoot.attribute(forName: "size")?.stringValue
                            let pkgPFSImageRootLinks = pkgPFSImageRoot.attribute(forName: "links")?.stringValue
                            let pkgPFSImageRootIMode = pkgPFSImageRoot.attribute(forName: "imode")?.stringValue
                            let pkgPFSImageRootIndex = pkgPFSImageRoot.attribute(forName: "index")?.stringValue
                            let pkgPFSImageRootINode = pkgPFSImageRoot.attribute(forName: "inode")?.stringValue
                            let pkgPFSImageRootName = pkgPFSImageRoot.attribute(forName: "name")?.stringValue
                        }

                        // Get the files in root
                        let pkgPFSImageRootFiles = PKGConfigurationXML.rootElement()?.elements(forName: "pfs-image").first?.elements(forName: "root").first?.elements(forName: "file") ?? []
                        for pkgPFSImageRootFile in pkgPFSImageRootFiles {
                            var newPS5PKGRootFile = PS5PKGRootFile()
                            newPS5PKGRootFile.FileSize = pkgPFSImageRootFile.attribute(forName: "size")?.stringValue
                            newPS5PKGRootFile.FilePlain = pkgPFSImageRootFile.attribute(forName: "plain")?.stringValue
                            newPS5PKGRootFile.FileCompression = pkgPFSImageRootFile.attribute(forName: "comp")?.stringValue
                            newPS5PKGRootFile.FileIMode = pkgPFSImageRootFile.attribute(forName: "imode")?.stringValue
                            newPS5PKGRootFile.FileIndex = pkgPFSImageRootFile.attribute(forName: "index")?.stringValue
                            newPS5PKGRootFile.FileINode = pkgPFSImageRootFile.attribute(forName: "inode")?.stringValue
                            newPS5PKGRootFile.FileName = pkgPFSImageRootFile.attribute(forName: "name")?.stringValue
                            PFSImageRootFiles.append(newPS5PKGRootFile)
                        }

                        // Get the directories in root
                        let pkgPFSImageRootDirectories = PKGConfigurationXML.rootElement()?.elements(forName: "pfs-image").first?.elements(forName: "root").first?.elements(forName: "dir") ?? []
                        for pkgPFSImageRootDirectory in pkgPFSImageRootDirectories {
                            var newPS5PKGRootDirectory = PS5PKGRootDirectory()
                            newPS5PKGRootDirectory.DirectorySize = pkgPFSImageRootDirectory.attribute(forName: "size")?.stringValue
                            newPS5PKGRootDirectory.DirectoryLinks = pkgPFSImageRootDirectory.attribute(forName: "links")?.stringValue
                            newPS5PKGRootDirectory.DirectoryIMode = pkgPFSImageRootDirectory.attribute(forName: "imode")?.stringValue
                            newPS5PKGRootDirectory.DirectoryIndex = pkgPFSImageRootDirectory.attribute(forName: "index")?.stringValue
                            newPS5PKGRootDirectory.DirectoryINode = pkgPFSImageRootDirectory.attribute(forName: "inode")?.stringValue
                            newPS5PKGRootDirectory.DirectoryName = pkgPFSImageRootDirectory.attribute(forName: "name")?.stringValue
                            PFSImageRootDirectories.append(newPS5PKGRootDirectory)
                        }
                        
                        // Get the files in uroot
                        if let pkgPFSImageURootFiles = PKGConfigurationXML.rootElement()?.elements(forName: "pfs-image").first?.elements(forName: "root").first?.elements(forName: "dir").first?.elements(forName: "file") {
                            for pkgPFSImageURootFile in pkgPFSImageURootFiles {
                                var newPS5PKGURootFile = PS5PKGRootFile()
                                newPS5PKGURootFile.FileSize = pkgPFSImageURootFile.attribute(forName: "size")?.stringValue
                                newPS5PKGURootFile.FilePlain = pkgPFSImageURootFile.attribute(forName: "plain")?.stringValue
                                newPS5PKGURootFile.FileCompression = pkgPFSImageURootFile.attribute(forName: "comp")?.stringValue
                                newPS5PKGURootFile.FileIMode = pkgPFSImageURootFile.attribute(forName: "imode")?.stringValue
                                newPS5PKGURootFile.FileIndex = pkgPFSImageURootFile.attribute(forName: "index")?.stringValue
                                newPS5PKGURootFile.FileINode = pkgPFSImageURootFile.attribute(forName: "inode")?.stringValue
                                newPS5PKGURootFile.FileName = pkgPFSImageURootFile.attribute(forName: "name")?.stringValue
                                PFSImageURootFiles.append(newPS5PKGURootFile)
                            }
                        }
                        
                        // Get the PKG nested image info
                        if let pkgNestedImage = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first {
                            let pkgNestedImageVersion = pkgNestedImage.attribute(forName: "version")?.stringValue
                            let pkgNestedImageReadOnly = pkgNestedImage.attribute(forName: "readonly")?.stringValue
                            let pkgNestedImageOffset = pkgNestedImage.attribute(forName: "offset")?.stringValue
                        }

                        // Get the PKG nested image sblock info
                        if let pkgNestedImageSBlock = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first?.elements(forName: "sblock").first {
                            let pkgPFSImageSBlockSigned = pkgNestedImageSBlock.attribute(forName: "signed")?.stringValue
                            let pkgPFSImageSBlockEncrypted = pkgNestedImageSBlock.attribute(forName: "encrypted")?.stringValue
                            let pkgPFSImageSBlockIgnoreCase = pkgNestedImageSBlock.attribute(forName: "ignore-case")?.stringValue
                            let pkgPFSImageSBlockIndexSize = pkgNestedImageSBlock.attribute(forName: "index-size")?.stringValue
                            let pkgPFSImageSBlockBlocks = pkgNestedImageSBlock.attribute(forName: "blocks")?.stringValue
                            let pkgPFSImageSBlockBackups = pkgNestedImageSBlock.attribute(forName: "backups")?.stringValue
                        }
                        if let pkgNestedImageSBlockImageSize = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first?.elements(forName: "sblock").first?.elements(forName: "image-size").first {
                            let pkgPFSImageSBlockImageSizeBlockSize = pkgNestedImageSBlockImageSize.attribute(forName: "block-size")?.stringValue
                            let pkgPFSImageSBlockImageSizeNum = pkgNestedImageSBlockImageSize.attribute(forName: "num")?.stringValue
                            let pkgPFSImageSBlockImageSizeValue = pkgNestedImageSBlockImageSize.stringValue
                        }
                        if let pkgNestedImageSBlockSuperInode = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first?.elements(forName: "sblock").first?.elements(forName: "super-inode").first {
                            let pkgPFSImageSBlockSuperInodeBlocks = pkgNestedImageSBlockSuperInode.attribute(forName: "blocks")?.stringValue
                            let pkgPFSImageSBlockSuperInodeInodes = pkgNestedImageSBlockSuperInode.attribute(forName: "inodes")?.stringValue
                            let pkgPFSImageSBlockSuperInodeRoot = pkgNestedImageSBlockSuperInode.attribute(forName: "root")?.stringValue
                        }
                        if let pkgNestedImageSBlockInode = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first?.elements(forName: "sblock").first?.elements(forName: "super-inode").first?.elements(forName: "inode").first {
                            let pkgPFSImageSBlockInodeSize = pkgNestedImageSBlockInode.attribute(forName: "size")?.stringValue
                            let pkgPFSImageSBlockInodeLinks = pkgNestedImageSBlockInode.attribute(forName: "links")?.stringValue
                            let pkgPFSImageSBlockInodeMode = pkgNestedImageSBlockInode.attribute(forName: "mode")?.stringValue
                            let pkgPFSImageSBlockInodeIMode = pkgNestedImageSBlockInode.attribute(forName: "imode")?.stringValue
                            let pkgPFSImageSBlockInodeIndex = pkgNestedImageSBlockInode.attribute(forName: "index")?.stringValue
                        }
                        // Get the PKG nested image metadata
                        if let pkgNestedImageMetadata = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first?.elements(forName: "metadata").first {
                            let pkgNestedImageMetadataSize = pkgNestedImageMetadata.attribute(forName: "size")?.stringValue
                            let pkgNestedImageMetadataPlain = pkgNestedImageMetadata.attribute(forName: "plain")?.stringValue
                            let pkgNestedImageMetadataCompression = pkgNestedImageMetadata.attribute(forName: "comp")?.stringValue
                            let pkgNestedImageMetadataOffset = pkgNestedImageMetadata.attribute(forName: "offset")?.stringValue
                            let pkgNestedImageMetadataPOffset = pkgNestedImageMetadata.attribute(forName: "poffset")?.stringValue
                            let pkgNestedImageMetadataAfid = pkgNestedImageMetadata.attribute(forName: "afid")?.stringValue
                        }
                        // Get the PKG nested image root info
                        if let pkgNestedImageRoot = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first?.elements(forName: "root").first {
                            let pkgPFSImageRootSize = pkgNestedImageRoot.attribute(forName: "size")?.stringValue
                            let pkgPFSImageRootLinks = pkgNestedImageRoot.attribute(forName: "links")?.stringValue
                            let pkgPFSImageRootIMode = pkgNestedImageRoot.attribute(forName: "imode")?.stringValue
                            let pkgPFSImageRootIndex = pkgNestedImageRoot.attribute(forName: "index")?.stringValue
                            let pkgPFSImageRootINode = pkgNestedImageRoot.attribute(forName: "inode")?.stringValue
                            let pkgPFSImageRootName = pkgNestedImageRoot.attribute(forName: "name")?.stringValue
                        }

                        // Get the files in root
                        if let pkgNestedImageRootFiles = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first?.elements(forName: "root").first?.elements(forName: "file") {
                            for pkgNestedImageRootFile in pkgNestedImageRootFiles {
                                var newPS5PKGRootFile = PS5PKGRootFile()
                                newPS5PKGRootFile.FileSize = pkgNestedImageRootFile.attribute(forName: "size")?.stringValue
                                newPS5PKGRootFile.FilePlain = pkgNestedImageRootFile.attribute(forName: "plain")?.stringValue
                                newPS5PKGRootFile.FileCompression = pkgNestedImageRootFile.attribute(forName: "comp")?.stringValue
                                newPS5PKGRootFile.FileIMode = pkgNestedImageRootFile.attribute(forName: "imode")?.stringValue
                                newPS5PKGRootFile.FileIndex = pkgNestedImageRootFile.attribute(forName: "index")?.stringValue
                                newPS5PKGRootFile.FileINode = pkgNestedImageRootFile.attribute(forName: "inode")?.stringValue
                                newPS5PKGRootFile.FileName = pkgNestedImageRootFile.attribute(forName: "name")?.stringValue
                                NestedImageRootFiles.append(newPS5PKGRootFile)
                            }
                        }            
                        // Get the directories in root
                        let pkgNestedImageRootDirectories = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first?.elements(forName: "root").first?.elements(forName: "dir") ?? []
                        for pkgNestedImageRootDirectory in pkgNestedImageRootDirectories {
                            var newPS5PKGRootDirectory = PS5PKGRootDirectory()
                            newPS5PKGRootDirectory.DirectorySize = pkgNestedImageRootDirectory.attribute(forName: "size")?.stringValue
                            newPS5PKGRootDirectory.DirectoryLinks = pkgNestedImageRootDirectory.attribute(forName: "links")?.stringValue
                            newPS5PKGRootDirectory.DirectoryIMode = pkgNestedImageRootDirectory.attribute(forName: "imode")?.stringValue
                            newPS5PKGRootDirectory.DirectoryIndex = pkgNestedImageRootDirectory.attribute(forName: "index")?.stringValue
                            newPS5PKGRootDirectory.DirectoryINode = pkgNestedImageRootDirectory.attribute(forName: "inode")?.stringValue
                            newPS5PKGRootDirectory.DirectoryName = pkgNestedImageRootDirectory.attribute(forName: "name")?.stringValue
                            NestedImageRootDirectories.append(newPS5PKGRootDirectory)
                        }

                        // Get the files in uroot
                        let pkgNestedImageURootFiles = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first?.elements(forName: "root").first?.elements(forName: "dir").first?.elements(forName: "file") ?? []
                        for pkgNestedImageURootFile in pkgNestedImageURootFiles {
                            var newPS5PKGURootFile = PS5PKGRootFile()
                            newPS5PKGURootFile.FileSize = pkgNestedImageURootFile.attribute(forName: "size")?.stringValue
                            newPS5PKGURootFile.FilePlain = pkgNestedImageURootFile.attribute(forName: "plain")?.stringValue
                            newPS5PKGURootFile.FileCompression = pkgNestedImageURootFile.attribute(forName: "comp")?.stringValue
                            newPS5PKGURootFile.FileIMode = pkgNestedImageURootFile.attribute(forName: "imode")?.stringValue
                            newPS5PKGURootFile.FileIndex = pkgNestedImageURootFile.attribute(forName: "index")?.stringValue
                            newPS5PKGURootFile.FileINode = pkgNestedImageURootFile.attribute(forName: "inode")?.stringValue
                            newPS5PKGURootFile.FileName = pkgNestedImageURootFile.attribute(forName: "name")?.stringValue
                            NestedImageURootFiles.append(newPS5PKGURootFile)
                        }
                        // Get the directories in uroot
                        let pkgNestedImageURootDirectories = PKGConfigurationXML.rootElement()?.elements(forName: "nested-image").first?.elements(forName: "root").first?.elements(forName: "dir").first?.elements(forName: "dir") ?? []
                        for pkgNestedImageURootDirectory in pkgNestedImageURootDirectories {
                            var newPS5PKGRootDirectory = PS5PKGRootDirectory()
                            newPS5PKGRootDirectory.DirectorySize = pkgNestedImageURootDirectory.attribute(forName: "size")?.stringValue
                            newPS5PKGRootDirectory.DirectoryLinks = pkgNestedImageURootDirectory.attribute(forName: "links")?.stringValue
                            newPS5PKGRootDirectory.DirectoryIMode = pkgNestedImageURootDirectory.attribute(forName: "imode")?.stringValue
                            newPS5PKGRootDirectory.DirectoryIndex = pkgNestedImageURootDirectory.attribute(forName: "index")?.stringValue
                            newPS5PKGRootDirectory.DirectoryINode = pkgNestedImageURootDirectory.attribute(forName: "inode")?.stringValue
                            newPS5PKGRootDirectory.DirectoryName = pkgNestedImageURootDirectory.attribute(forName: "name")?.stringValue
                            NestedImageURootDirectories.append(newPS5PKGRootDirectory)
                        }
                        
                        // Set default CurrentFileList & CurrentDirectoryList and reload data
                        CurrentFileList = PFSImageRootFiles
                        CurrentDirectoryList = PFSImageRootDirectories
                        PKGImageFilesTableView.reloadData()
                        PKGImageDirectoriesTableView.reloadData()
                        
                        // Get param.json, icon0.png & pic0.png PKG entry info from the package configuration
                        var paramJsonPKGEntry = PS5PKGEntry()
                        var icon0PKGEntry = PS5PKGEntry()
                        var pic0PKGEntry = PS5PKGEntry()
                        
                        for pkgEntry in PKGConfigurationXML.rootElement()?.elements(forName: "entries").flatMap({ $0.elements(forName: "entry") }) ?? [] {
                            if let entryName = pkgEntry.attribute(forName: "name")?.stringValue {
                                switch entryName {
                                case "param.json":
                                    paramJsonPKGEntry.EntryOffset = pkgEntry.attribute(forName: "offset")?.stringValue
                                    paramJsonPKGEntry.EntrySize = pkgEntry.attribute(forName: "size")?.stringValue
                                    paramJsonPKGEntry.EntryName = entryName
                                case "icon0.png":
                                    icon0PKGEntry.EntryOffset = pkgEntry.attribute(forName: "offset")?.stringValue
                                    icon0PKGEntry.EntrySize = pkgEntry.attribute(forName: "size")?.stringValue
                                    icon0PKGEntry.EntryName = entryName
                                case "pic0.png":
                                    pic0PKGEntry.EntryOffset = pkgEntry.attribute(forName: "offset")?.stringValue
                                    pic0PKGEntry.EntrySize = pkgEntry.attribute(forName: "size")?.stringValue
                                    pic0PKGEntry.EntryName = entryName
                                default:
                                    break
                                }
                            }
                        }
                        
                        // Extract the files from the pkg file
                        do {
                            // Seek from the end
                            let pkgReader = try FileHandle(forReadingFrom: pkgURL)
                            pkgReader.seekToEndOfFile()

                            var containerOffsetDecimalValue: Int64 = 0
                            var entryOffsetDecimalValue: Int64 = 0
                            var entrySizeDecimalValue: Int64 = 0
                            
                            var paramJSONOffsetPosition: Int64 = 0
                            var icon0OffsetPosition: Int64 = 0
                            var pic0OffsetPosition: Int64 = 0
 
                            // PARAM.JSON
                            // Get decimal offset values
                            containerOffsetDecimalValue = Int64(pkgMountImageContainerOffset!.replacingOccurrences(of: "0x", with: ""), radix: 16) ?? 0
                            entryOffsetDecimalValue = Int64(paramJsonPKGEntry.EntryOffset!.replacingOccurrences(of: "0x", with: ""), radix: 16) ?? 0
                            entrySizeDecimalValue = Int64(paramJsonPKGEntry.EntrySize!.replacingOccurrences(of: "0x", with: ""), radix: 16) ?? 0

                            paramJSONOffsetPosition = containerOffsetDecimalValue + entryOffsetDecimalValue
                            
                            // Seek to the beginning of the param.json file and read
                            try pkgReader.seek(toOffset: UInt64(paramJSONOffsetPosition))
                            let paramFileBuffer = pkgReader.readData(ofLength: Int(entrySizeDecimalValue))
                            
                            if let currentParamJSON = String(data: paramFileBuffer, encoding: .utf8), !currentParamJSON.isEmpty {
                                
                                CurrentParamJSON = currentParamJSON
                                
                                let PS5ParamData = try JSONDecoder().decode(PS5Param.self, from: currentParamJSON.data(using: .utf8)!)
                                let NewPS5GameOrApp: PS5GameOrApp = PS5GameOrApp(GameAppPath: pkgURL.path,
                                                                                 GameAppTitle: PS5ParamData.localizedParameters!.enUS!.titleName,
                                                                                 GameAppID: PS5ParamData.titleID,
                                                                                 GameAppContentID: PS5ParamData.contentID,
                                                                                 GameAppVersion: PS5ParamData.contentVersion,
                                                                                 GameAppRequiredFirmware: PS5ParamData.requiredSystemSoftwareVersion)
                                
                                GameTitleTextField.stringValue = NewPS5GameOrApp.GameAppTitle!
                                GameIDTextField.stringValue = "ID: " + NewPS5GameOrApp.GameAppID!
                                GameContentIDTextField.stringValue = "Content ID: " + NewPS5GameOrApp.GameAppContentID!
                                GameVersionTextField.stringValue = "Version: " + NewPS5GameOrApp.GameAppVersion!
                                GameRegionTextField.stringValue = GetRegion(ID: NewPS5GameOrApp.GameAppID!)
                                
                                var AdjustedReqFw = NewPS5GameOrApp.GameAppRequiredFirmware!.replacingOccurrences(of: "0x", with: "")
                                AdjustedReqFw.insert(".", at: AdjustedReqFw.index(AdjustedReqFw.startIndex, offsetBy: 2))
                                AdjustedReqFw.insert(".", at: AdjustedReqFw.index(AdjustedReqFw.startIndex, offsetBy: 5))
                                AdjustedReqFw.insert(".", at: AdjustedReqFw.index(AdjustedReqFw.startIndex, offsetBy: 8))
                                GameRequiredFirmwareTextField.stringValue = "Required Firmware: " + AdjustedReqFw
                                
                                let fileAttributes = try? FileManager.default.attributesOfItem(atPath: pkgURL.path)
                                if let fileSize = fileAttributes?[.size] as? NSNumber {
                                    let gameSize = String(format: "Size: %.2f GB", fileSize.doubleValue / 1073741824)
                                    GameSizeTextField.stringValue = gameSize
                                }
                                
                                // Determine category
                                switch PS5ParamData.applicationCategoryType {
                                case 0:
                                    GameCategoryTextField.stringValue = "Category: Game"
                                case 65792:
                                    GameCategoryTextField.stringValue = "Category: RNPS Media App"
                                case 131328:
                                    GameCategoryTextField.stringValue = "Category: System Built-in App"
                                case 131584:
                                    GameCategoryTextField.stringValue = "Category: Big Daemon"
                                case 16777216:
                                    GameCategoryTextField.stringValue = "Category: ShellUI"
                                case 33554432:
                                    GameCategoryTextField.stringValue = "Category: Daemon"
                                case 67108864:
                                    GameCategoryTextField.stringValue = "Category: ShellApp"
                                default:
                                    GameCategoryTextField.stringValue = "Category: Unknown"
                                }
                            }
                            
                            // ICON0.PNG
                            containerOffsetDecimalValue = Int64(pkgMountImageContainerOffset!.replacingOccurrences(of: "0x", with: ""), radix: 16) ?? 0
                            entryOffsetDecimalValue = Int64(icon0PKGEntry.EntryOffset!.replacingOccurrences(of: "0x", with: ""), radix: 16) ?? 0
                            entrySizeDecimalValue = Int64(icon0PKGEntry.EntrySize!.replacingOccurrences(of: "0x", with: ""), radix: 16) ?? 0
                            icon0OffsetPosition = containerOffsetDecimalValue + entryOffsetDecimalValue
                            
                            print(entryOffsetDecimalValue)
                            print(entrySizeDecimalValue)
                            print(icon0OffsetPosition)

                            // Seek to the beginning of the icon0.png file and read
                            try pkgReader.seek(toOffset: UInt64(icon0OffsetPosition))
                            let icon0FileBuffer = pkgReader.readData(ofLength: Int(entrySizeDecimalValue))
                                
                            // Check the buffer and display the icon
                            if !icon0FileBuffer.isEmpty {
                                let icon0BitmapImage = NSImage(data: Data(icon0FileBuffer))
                                if let icon0BitmapImage = icon0BitmapImage {
                                    GameIconImageView.image = icon0BitmapImage
                                    CurrentIcon0 = icon0BitmapImage
                                }
                            }

                            // PIC0.PNG
                            entryOffsetDecimalValue = Int64(pic0PKGEntry.EntryOffset!.replacingOccurrences(of: "0x", with: ""), radix: 16) ?? 0
                            entrySizeDecimalValue = Int64(pic0PKGEntry.EntrySize!.replacingOccurrences(of: "0x", with: ""), radix: 16) ?? 0
                            pic0OffsetPosition = containerOffsetDecimalValue + entryOffsetDecimalValue
                            
                            print(entryOffsetDecimalValue)
                            print(entrySizeDecimalValue)
                            print(pic0OffsetPosition)

                            // Seek to the beginning of the pic0.png file and read
                            try pkgReader.seek(toOffset: UInt64(pic0OffsetPosition))
                            let pic0FileBuffer = pkgReader.readData(ofLength: Int(entrySizeDecimalValue))
                            
                            // Check the buffer and display the icon
                            if !pic0FileBuffer.isEmpty {
                                let pic0BitmapImage = NSImage(data: Data(pic0FileBuffer))
                                if let pic0BitmapImage = pic0BitmapImage {
                                    CurrentPic0 = pic0BitmapImage
                                    //self.view.window!.backgroundColor = NSColor(patternImage: pic0BitmapImage)
                                }
                            }
                            
                            try pkgReader.close()
                        } catch {
                            return
                        }
                        
                    }
                }
            }
            
        }

    }
    
    @IBAction func ShowPFSImageFiles(_ sender: NSButton) {
        HideTableViews()
        
        // Set new file list and show the table
        CurrentListTextField.stringValue = "PKG PFS Image Files :"
        CurrentFileList = PFSImageRootFiles
        PKGImageFilesScrollView.isHidden = false
    }
    
    @IBAction func ShowPFSImageDirectories(_ sender: NSButton) {
        HideTableViews()
        
        // Set new file list and show the table
        CurrentListTextField.stringValue = "PKG PFS Image Directories :"
        CurrentDirectoryList = PFSImageRootDirectories
        PKGImageDirectoriesScrollView.isHidden = false
    }
    
    @IBAction func ShowNestedImageFiles(_ sender: NSButton) {
        HideTableViews()
        
        // Set new file list and show the table
        CurrentListTextField.stringValue = "PKG Nested Image Files :"
        CurrentFileList = NestedImageRootFiles
        PKGImageFilesScrollView.isHidden = false
    }
    
    @IBAction func ShowNestedImageDirectories(_ sender: NSButton) {
        HideTableViews()
        
        // Set new file list and show the table
        CurrentListTextField.stringValue = "PKG Nested Image Directories :"
        CurrentDirectoryList = NestedImageRootDirectories
        PKGImageDirectoriesScrollView.isHidden = false
    }
    
    @IBAction func ShowEntries(_ sender: NSButton) {
        HideTableViews()
        
        // Set new file list and show the table
        CurrentListTextField.stringValue = "PKG Entries :"
        PKGEntriesScrollView.isHidden = false
    }
    
    @IBAction func ShowScenarios(_ sender: NSButton) {
        HideTableViews()
        
        // Set new file list and show the table
        CurrentListTextField.stringValue = "PKG Scenarios :"
        PKGScenariosScrollView.isHidden = false
    }
    
    @IBAction func ShowChunks(_ sender: NSButton) {
        HideTableViews()
        
        // Set new file list and show the table
        CurrentListTextField.stringValue = "PKG Chunks :"
        PKGChunksScrollView.isHidden = false
    }
    
    @IBAction func ShowOuters(_ sender: NSButton) {
        HideTableViews()
        
        // Set new file list and show the table
        CurrentListTextField.stringValue = "PKG Outers :"
        PKGOutersScrollView.isHidden = false
    }
    
    @IBAction func ExportConfigurationXML(_ sender: NSButton) {
        if CurrentConfigurationXML != nil {
            let saveDialog = NSSavePanel()
            saveDialog.title = "Select a save path"
            saveDialog.allowedFileTypes = ["xml"]
            saveDialog.nameFieldStringValue = "package-configuration.xml"
            
            if saveDialog.runModal() == NSApplication.ModalResponse.OK {
                if let fileURL = saveDialog.url {
                    try? CurrentConfigurationXML?.xmlData.write(to: fileURL)
                }
            }
        }
    }
    
    @IBAction func ExportParamJSON(_ sender: NSButton) {
        if let paramJSON = CurrentParamJSON, !paramJSON.isEmpty {
            let savePanel = NSSavePanel()
            savePanel.title = "Select a save path"
            savePanel.allowedFileTypes = ["json"]
            savePanel.nameFieldStringValue = "param.json"
            
            savePanel.begin { result in
                if result == NSApplication.ModalResponse.OK, let fileURL = savePanel.url {
                    do {
                        try paramJSON.write(to: fileURL, atomically: true, encoding: .utf8)
                    } catch {
                        return
                    }
                }
            }
        }
    }
    
    @IBAction func ExportIconPNG(_ sender: NSButton) {
        if CurrentIcon0 != nil {
            let savePanel = NSSavePanel()
            savePanel.title = "Select a save path"
            savePanel.allowedFileTypes = ["png"]
            savePanel.nameFieldStringValue = "icon0.png"
            
            if savePanel.runModal() == .OK {
                guard let fileURL = savePanel.url else { return }
                
                if let pngData = CurrentIcon0!.tiffRepresentation,
                   let bitmapImage = NSBitmapImageRep(data: pngData),
                   let pngDataOutput = bitmapImage.representation(using: .png, properties: [:]) {
                    do {
                        try pngDataOutput.write(to: fileURL)
                    } catch {
                        return
                    }
                }
            }
        }
    }
    
    @IBAction func ExportBackgroundPNG(_ sender: NSButton) {
        if CurrentPic0 != nil {
            let savePanel = NSSavePanel()
            savePanel.title = "Select a save path"
            savePanel.allowedFileTypes = ["png"]
            savePanel.nameFieldStringValue = "pic0.png"
            
            if savePanel.runModal() == .OK {
                guard let fileURL = savePanel.url else { return }
                
                if let pngData = CurrentPic0!.tiffRepresentation,
                   let bitmapImage = NSBitmapImageRep(data: pngData),
                   let pngDataOutput = bitmapImage.representation(using: .png, properties: [:]) {
                    do {
                        try pngDataOutput.write(to: fileURL)
                    } catch {
                        return
                    }
                }
            }
        }
    }
    
    
    func FindOffsetsInFile(filePath: String, startString: String, endString: String) -> [Int64] {
        guard FileManager.default.fileExists(atPath: filePath) else {
            return []
        }

        let startBytes = Data(startString.utf8)
        let endBytes = Data(endString.utf8)

        var startOffset: Int64 = -1
        var endOffset: Int64 = -1

        do {
            let fileHandle = try FileHandle(forReadingFrom: URL(fileURLWithPath: filePath))
            let fileLength = Int64(try fileHandle.seekToEnd())
            let bufferSize = 4096

            var buffer = Data()
            
            var totalBytesRead: Int64 = fileLength
            while totalBytesRead > 0 {
                let bytesRead = min(bufferSize, Int(totalBytesRead))
                totalBytesRead -= Int64(bytesRead)
                fileHandle.seek(toFileOffset: UInt64(totalBytesRead))
                let chunk = fileHandle.readData(ofLength: bytesRead)
                buffer.insert(contentsOf: chunk, at: 0)
                
                if let foundEndRange = buffer.range(of: endBytes) {
                    endOffset = totalBytesRead + Int64(foundEndRange.lowerBound)
                }
                if let foundStartRange = buffer.range(of: startBytes) {
                    startOffset = totalBytesRead + Int64(foundStartRange.lowerBound)
                }
                
                if startOffset != -1 && endOffset != -1 {
                    break
                }
                
                buffer = Data(buffer.prefix(bufferSize))
            }
            
            fileHandle.closeFile()
            
            // Return the start and end offset
            return [startOffset, endOffset]
            
        } catch {
            return []
        }
    }
    
    func ExtractData(FilePath: String, StartOffset: Int64, EndOffset: Int64, FileName: String? = "") -> String? {
        let bufferSize = 4096

        do {
            let NewFileHandle = try FileHandle(forReadingFrom: URL(fileURLWithPath: FilePath))
            let ExtractedDataSize = EndOffset - StartOffset
            try NewFileHandle.seek(toOffset: UInt64(StartOffset))

            var NewExtractedData = Data()
            
            var RemainingDataSize = Int(ExtractedDataSize)
            while RemainingDataSize > 0 {
                let readSize = min(bufferSize, RemainingDataSize)
                let chunk = NewFileHandle.readData(ofLength: readSize)
                NewExtractedData.append(chunk)
                RemainingDataSize -= readSize
            }
            
            NewFileHandle.closeFile()
            
            // Process the extracted data
            if FileName != nil {
                if FileName == "param.json" {
                    if let ExtractedData = String(data: NewExtractedData, encoding: .utf8) {
                        var ParamJSONData = ExtractedData.split(whereSeparator: \.isNewline)
                        
                        // Adjust the output
                        if !ParamJSONData.isEmpty {
                            ParamJSONData.removeFirst()
                            ParamJSONData.insert("{", at: 0)
                            if let lastElementIndex = ParamJSONData.indices.last {
                                ParamJSONData[lastElementIndex] += "version.xml\""
                            }
                            ParamJSONData.append("}")
                        }

                        let FinalParamJSONString = ParamJSONData.joined(separator: "\n")
                        return FinalParamJSONString
                    } else {
                        return nil
                    }
                } else if FileName == "package-configuration.xml" {
                    if let ExtractedData = String(data: NewExtractedData, encoding: .utf8) {
                        let ExtractedPKGConfigurationData = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + ExtractedData.dropLast() + "\n</package-configuration>"
                        return ExtractedPKGConfigurationData
                    } else {
                        return nil
                    }
                } else if FileName == "icon0.png" {
                    return nil
                } else if FileName == "pic0.png" {
                    return nil
                } else {
                    return nil
                }
            } else {
                return nil
            }
            
        } catch {
            return nil
        }
    }
    
    func numberOfRows(in tableView: NSTableView) -> Int {
        if tableView.identifier == NSUserInterfaceItemIdentifier(rawValue: "PKGEntriesTableView") {
            return PKGEntries.count
        }
        else if tableView.identifier == NSUserInterfaceItemIdentifier(rawValue: "PKGScenariosTableView") {
            return PKGScenarios.count
        }
        else if tableView.identifier == NSUserInterfaceItemIdentifier(rawValue: "PKGChunksTableView") {
            return PKGChunks.count
        }
        else if tableView.identifier == NSUserInterfaceItemIdentifier(rawValue: "PKGOutersTableView") {
            return PKGOuters.count
        }
        else if tableView.identifier == NSUserInterfaceItemIdentifier(rawValue: "PKGImageFilesTableView") {
            return CurrentFileList.count
        }
        else if tableView.identifier == NSUserInterfaceItemIdentifier(rawValue: "PKGImageDirectoriesTableView") {
            return CurrentDirectoryList.count
        }
        else {
            return 0
        }
    }
    
    func tableView(_ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int) -> NSView? {
        // Entries
        if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "EntryOffsetColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGEntries[row].EntryOffset ?? ""
            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "EntrySizeColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGEntries[row].EntrySize ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "EntryNameColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGEntries[row].EntryName ?? ""

            return cellView
            
        // Scenarios
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ScenarioIDColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGScenarios[row].ScenarioID ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ScenarioTypeColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGScenarios[row].ScenarioType ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ScenarioNameColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGScenarios[row].ScenarioName ?? ""

            return cellView
            
        // Chunks
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ChunkIDColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGChunks[row].ChunkID ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ChunkFlagColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGChunks[row].ChunkFlag ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ChunkLocusColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGChunks[row].ChunkLocus ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ChunkLanguageColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGChunks[row].ChunkLanguage ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ChunkDispsColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGChunks[row].ChunkDisps ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ChunkNumColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGChunks[row].ChunkNum ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ChunkSizeColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGChunks[row].ChunkSize ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ChunkNameColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGChunks[row].ChunkName ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "ChunkValueColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGChunks[row].ChunkValue ?? ""

            return cellView
            
        // Outers
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "OuterIDColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGOuters[row].OuterID ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "OuterImageColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGOuters[row].OuterImage ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "OuterOffsetColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGOuters[row].OuterOffset ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "OuterSizeColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGOuters[row].OuterSize ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "OuterChunksColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = PKGOuters[row].OuterChunks ?? ""

            return cellView
            
        // Image files
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "FileSizeColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentFileList[row].FileSize ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "FilePlainColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentFileList[row].FilePlain ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "FileCompressionColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentFileList[row].FileCompression ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "FileiModeColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentFileList[row].FileIMode ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "FileIndexColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentFileList[row].FileIndex ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "FileiNodeColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentFileList[row].FileINode ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "FileNameColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentFileList[row].FileName ?? ""

            return cellView
            
        // Image directories
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "DirectorySizeColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentDirectoryList[row].DirectorySize ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "DirectoryLinksColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentDirectoryList[row].DirectoryLinks ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "DirectoryiModeColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentDirectoryList[row].DirectoryIMode ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "DirectoryIndexColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentDirectoryList[row].DirectoryIndex ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "DirectoryiNodeColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentDirectoryList[row].DirectoryINode ?? ""

            return cellView
        } else if tableColumn?.identifier == NSUserInterfaceItemIdentifier(rawValue: "DirectoryNameColumn") {
            guard let cellView = tableView.makeView(withIdentifier: tableColumn!.identifier, owner: self) as? NSTableCellView else { return nil }
            cellView.textField?.stringValue = CurrentDirectoryList[row].DirectoryName ?? ""

            return cellView
        } else {
            return nil
        }
    }
    
    func HideTableViews() {
        PKGEntriesScrollView.isHidden = true
        PKGScenariosScrollView.isHidden = true
        PKGChunksScrollView.isHidden = true
        PKGOutersScrollView.isHidden = true
        PKGImageFilesScrollView.isHidden = true
        PKGImageDirectoriesScrollView.isHidden = true
    }
    
    struct PS5GameOrApp {
        var GameAppPath: String?
        var GameAppTitle: String?
        var GameAppID: String?
        var GameAppRegion: String?
        var GameAppContentID: String?
        var GameAppSize: String?
        var GameAppVersion: String?
        var GameAppRequiredFirmware: String?
    }
    
    struct PS5PKGEntry {
        var EntryOffset: String?
        var EntrySize: String?
        var EntryName: String?
    }
    
    struct PS5PKGScenario {
        var ScenarioName: String?
        var ScenarioType: String?
        var ScenarioID: String?
    }
    
    struct PS5PKGChunk {
        var ChunkID: String?
        var ChunkFlag: String?
        var ChunkLocus: String?
        var ChunkName: String?
        var ChunkSize: String?
        var ChunkNum: String?
        var ChunkDisps: String?
        var ChunkLanguage: String?
        var ChunkValue: String?
    }
    
    struct PS5PKGOuter {
        var OuterChunks: String?
        var OuterSize: String?
        var OuterOffset: String?
        var OuterImage: String?
        var OuterID: String?
    }
    
    struct PS5PKGRootDirectory {
        var DirectoryName: String?
        var DirectoryINode: String?
        var DirectoryIndex: String?
        var DirectoryIMode: String?
        var DirectoryLinks: String?
        var DirectorySize: String?
    }
    
    struct PS5PKGRootFile {
        var FileName: String?
        var FileINode: String?
        var FileIndex: String?
        var FileIMode: String?
        var FileCompression: String?
        var FilePlain: String?
        var FileSize: String?
    }
    
    func GetRegion(ID: String) -> String {
        if ID.hasPrefix("PPSA") {
            return "Region: NA / Europe"
        } else if ID.hasPrefix("ECAS") {
            return "Region: Asia"
        } else if ID.hasPrefix("ELAS") {
            return "Region: Asia"
        } else if ID.hasPrefix("ELJM") {
            return "Region: Japan"
        } else {
            return "Region: Unknown"
        }
    }

}

