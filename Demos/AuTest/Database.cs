// ────── ╔╗
// ╔═╦╦═╦╦╬╣ Database.cs
// ║║║║╬║╔╣║ <<TODO>>
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using Nori;
namespace Flux;

#pragma warning disable 649
public enum EBGSystem {
   T6Axis = 6, T2Axis = 2, T4Axis = 4, T5Axis = 5, T5AxisTandem = 7, T2AxisTandem = 8,
   T4AxisTandem = 9, T6AxisTandem = 10
}

public enum SH : int {
   Nil = 0, InnerCCW = 1, OuterCCW = 2, AppLength = 3, AppRadius = 4,
   AppAngle = 5, EscLength = 6, EscRadius = 7, EscAngle = 8,
   ReverseRepo = 10, CornerjointWidth = 11, WirejointWidth = 12, RepositionLate = 13, RepoBackHome = 14,
   MaxScallop = 15, MinPunchOverlap = 16, MaxPunchOverlap = 17, WirejointSpacing = 18, ClampSafeToolChange = 19,
   SheetSafeToolChange = 20, ClampSafeTraverse = 21, PunchOnBendLines = 22, DistFromBendEnd = 23, FrontBendTool = 24,
   BackBendTool = 25, DivideOuterCutForForming = 26, NibblePitch = 27, UseNibblePitch = 28, MaxTOverhang = 29,
   MaxPOverhang = 30, ToolCheckSheetThickness = 31, UseJointTool = 32, JointTool = 33, ClampTurretSpace = 34,
   CornerSlugoutType = 35, ApproachTypes = 38, OpenPlineProcess = 39,
   NibbleLine = 40, NibbleArc = 41, CornerJointType = 42, JointToolJointType = 43, MinSegLenToTool = 44,
   SlugoutWithRectTool = 45, CircleStartAng = 46, RouteMaxPenalty = 47, ShowBendAid = 48, BreakThinMacro = 49,
   ThinMacroFactor = 50, AddMoreFrames = 51, SinglePunchLimit = 52, MaxSlugoutSize = 53, MaxSlugoutArea = 54,
   MinCJLength = 55, ToolCollectionName = 56, CutNotchesOutFirst = 57,
   SplFinalPunch = 68, SplFinalPunchTool = 69,
   SplFinalPunchCorner = 70, SplFinalPunchMinLen = 71, ChamferMaxToolSize = 72, ChamferMaxLateral = 73, ChamferMinOverhang = 74,
   HemPrebendAngle = 75, BendToolShorterBy = 78,
   BGAlwaysPullbackThickness = 82, ACB2SensorLength = 83, ACB3SensorLength = 84,
   OpenPDGinMCAM = 87, MutePoint = 89,
   RamVPinch = 90, BendGAP2D = 91, BendGAP3D = 92, FoldGAP2D = 93, FoldGAP3D = 94,
   BendSafetyMethod = 95, BGTransition = 97,
   BGTransitionDelay = 100, BGXSpeed = 101, BGY1Speed = 102, BGY2Speed = 103, BGZ1Speed = 104,
   RamVClosing = 105, RamPressTime = 106, RamVBend = 107, AngleCorrection = 108,
   BGZ2Speed = 110, RamOpenPosition = 112, RamVOpen = 113, RamZDecompress = 114,
   RamVDecompress = 115, FillColor = 116, StrokeColor = 117, StrokeWidth = 118, TextColor = 119,
   FontFamily = 120, Bold = 121, Italic = 122, FontSize = 123, TextAlignment = 124,
   Underline = 125, ShowBMachineFrame = 126, CmdBarCaption = 127, ShowBGMechanism = 128, UseExteriorAngle = 129,
   ShowBRam = 130, ShowBDieRail = 131, ShowBClamps = 132, ShowBendGuard = 133, ShowBFloor = 134,
   DimArrowSize = 135, DimExtend = 136, DimLinDecimals = 137, DimAngDecimals = 138, DimOffset = 139,
   DimTxtSize = 140, DimTxtPos = 141, DimFontname = 143, RamZOpen = 144,
   RamZSafety = 145, ZRelease = 146, DXFWhiteToBlack = 147, SubSheaf = 148, StitchThreshold = 149,
   DXFUnit = 150, IgnoreLayers = 151, Language = 152, Unit = 153, RecentCutMC = 154,
   ShearedSheet = 155, PFRound = 156, PFSquare = 157, PFRectWidth = 158, PFRectLength = 159,
   PFObroundWidth = 160, PFObroundLength = 161, PFOther = 162, MinOverhang = 163, PFNotchWidth = 164,
   OutNoPolyline = 165, OutBendinfo = 166, OutBlackToGray = 167, UIDecimals = 168, RecentBendMC = 169,
   SheetSize = 171, ClampsPos = 172, SheetMargin = 173, PartGap = 174,
   NestStartCorner = 175, TryCommonLine = 176, TryRotate90 = 177, LayerUses = 179,
   DXFInteriorAngle = 180, HemPosition = 181, Pierce = 182, UseLaserMacros = 183, RecentMatl = 184,
   RecentThickness = 185, MovePiercePts = 186, RouteAroundHoles = 187, RouteSmallHoleThreshold = 188, RouteAllowance = 189,
   NoPierceZone = 190, StitchCutDist = 193, PickBox = 194,
   RepMargin = 195, RepGutter = 196, PageSize = 197, RepDecimals = 198, HeaderFontFamily = 199,
   HeaderBold = 200, HeaderItalic = 201, HeaderFontSize = 202, HeaderUnderline = 203, HeaderTextColor = 204,
   BgrdColor = 205, DPI = 206, No3DPDF = 207, RamPinchCorrect = 208, RamPinchDelay = 209,
   RamZRelease = 210, RamBendDelay = 212, BendRepTemplate = 214,
   BendRepDestination = 215, BendRepProcessor = 216, BendCodeDestination = 217, BendCodeProcessor = 218, ShowBConveyor = 219,
   ShowLoadMaster = 220, ShowBRobotRail = 221, ShowBRobot = 222, LayoutRepTemplate = 224,
   LayoutRepDestination = 225, LayoutRepProcessor = 226, LayoutCodeDestination = 227, LayoutCodeProcessor = 228, BridgeCut = 229,
   RecentFoldMC = 230, FoldRepTemplate = 231, FoldRepDestination = 232, FoldRepProcessor = 233, FoldCodeDestination = 234,
   FoldCodeProcessor = 235, InfiniteBInventory = 236, DarkenColors = 237, ShowSequenceLines = 238, BGXSlack = 239,
   FCFrame = 240, FUpperJaw = 241, FLowerJaw = 242, FUpperBlade = 243, FLowerBlade = 244,
   FENWTools = 245, FZBWBlades = 246, FLowerSupports = 247, ShowBendBG = 248, TeighaPath = 249,
   PowerPierceDist = 250, MinDistFromAppBaseToCorner = 251, ApproachOverCenter = 253, ChuteAllowance = 254,
   ChuteComplexity = 255, WirejointSets = 256, FinishRules = 257, FGrippers = 258, ChooseCCBy = 259,
   ClampMove = 261, AddFrames = 262, LaserSeq = 263, LaserProcessSeq = 264,
   PrePierceReverseSeq = 265, FinalToolingTogether = 266, BridgeCutBlock = 267, BridgeWidth = 268, MaxBridgeLength = 269,
   BridgeMinDistFromCorner = 271, CLCSequence = 272, EnableBridgeCut = 273, SnapPierceToQuadrant = 274,
   PrePiercePartwise = 275, HoleConnectedCommonOuter = 276, ElementSort = 277, ElemSortStart = 278, ElemSortBeltWidth = 279,
   ElemSortLAA = 280, RaiseHeadOverOuterContour = 282, PierceOrient = 283, AltBezierDraw = 284,
   ExplodeBlock2D = 286, ShowBACBLaser = 287, Selector = 288, BendNCPictures = 289,
   IgnoreBToolHints = 290, ShowBOverbending = 291, BendCodeAutogen = 292, BGParallelRetract = 293, BGMovePath = 294,
   ShowBPunch = 295, ShowBDie = 296, ShowBWorkpiece = 297, BendRepFormat = 298, BendRepAutogen = 299,
   RamAutoDecompress = 300, BendCalc = 301, FootSwitch = 302, Comments = 303, PartID = 304,
   Customer = 305, Author = 306, JobNumber = 307, Surface = 308, Treatment = 309,
   Film = 310, DwgFillPart = 311, DwgShowOpenEnds = 312, DwgShowCenterMarkers = 313, Assembly = 314,
   BendFXWithNC = 315, BendFXDestination = 316, Tags = 317, MatlNames = 318, HemMutePoint = 319,
   BGClampStrategy = 320, ZbwTLInstalled = 321, ZbwTRInstalled = 322, ZbwBLInstalled = 323, ZbwBRInstalled = 324,
   ZbwTLLength = 325, ZbwTRLength = 326, ZbwBLLength = 327, ZbwBRLength = 328, ZbwTLPosition = 329,
   ZbwTRPosition = 330, ZbwBLPosition = 331, ZbwBRPosition = 332, BendNCPictureNoHoles = 333, BendFlatFormat = 334,
   BendFlatDestination = 335, ACBLInstalled = 336, ACBUInstalled = 337, SkipNoBending = 338, SplineConvert = 339,
   SplineConvertN = 340, SplineConvertPitch = 341, SplineConvertDeviation = 342, MaxThickness = 343, ShowComponents = 344,
   EBCell = 345, TrackMinX = 346, TrackMaxX = 347, IGESviaHOOPS = 348, BTablePosition = 349,
   BGInstalled = 350, HasFoldRobot = 351, RobotPosY = 353, RobotPosZ = 354,
   YRegripStn = 355, MinAirgap = 356, ReverseMouseWheel = 357, TessLOD = 358, FGXSlack = 359,
   AlwaysTryRecenter = 360, RegripSwivelSide = 361, BendRepAttachToNC = 362, AirGapTable = 363, ElemSortDirection = 364,
   LayoutName = 365, LayoutCopies = 366, TrumpfAnalyticsOn = 367, CornerDetours = 368, NoCornerProcAngle = 369,
   CornerDwell = 370, CornerDwellTime = 371, CornerRoundingRadius = 372, CornerLoopingRadius = 373, CornerRoundingTolerance = 374,
   MaxLoopingExtension = 375, Grain = 376, SheetGrain = 377, BackCutOpenPlines = 378, ACBwithZBW = 379,
   ACBwithENW = 380, ScrapGridSize = 381, ScrapGridAppLength = 382, NestInsideHoles = 383, SliceSheet = 384,
   SliceSheetXSpacing = 385, SliceSheetYSpacing = 386, CreateRemnantSheet = 387, MinRemnantSheetWidth = 388, FinalCutXOffset = 389,
   SliceJointGapSheet = 390, SliceJointGapPart = 391, SlicePierceDistFromPart = 392, SliceMeasureDist = 393, SliceOvertravel = 394,
   SliceAtEnd = 395, ReducedApp = 396, ACBSystems = 397, EscapeOn = 398, PlatesAlongX = 399,
   PlatesAlongY = 400, MountOffset = 401, RibThickness = 402, BaseThickness = 403, RibGridPadding = 404,
   BasePlateMargin = 405, RamMinZOpen = 406, ShowSlatPins = 407, SlatDistance = 408, SupportPinDistance = 409,
   FirstSlatOffset = 410, MovePiercePtsForTipping = 411, OvershootHeight = 412, ShowBRegripStn = 413, DwellAfterApproach = 414,
   PreCutLength = 416, MaxDipIntoSlats = 417, MaxMicrojoints = 418, ShtLoadErrorX = 419,
   ShtLoadErrorY = 420, ShtLoadErrorAng = 421, RouteAllowanceTipping = 422, MicrojointNestedHoles = 423, UsingKB40 = 424,
   LayRepFormat = 425, ViewpointX = 426, ViewpointZ = 427, LayoutNCUnit = 428, FoldFlatFormat = 429,
   FoldFlatDestination = 430, FoldFGStrategy = 431, ShowBGripper = 432, BAUpSpeed = 433, BADownSpeed = 434,
   BGModeOverride = 435, HemRecenterThickness = 436, HemRecenterLength = 437, MeasureSheetPos = 438, MeasureCorner = 439,
   SheetLoadingType = 440, CutWithSingleHead = 441, CheckBToolOverload = 442, ShowBCellComponents = 443, ShowBPallets = 444,
   PickupGEOSide = 445, JobRepTemplate = 446, SpreadOutLastLayout = 447, MarkingRules = 448, BG1Variant = 449,
   BG2Variant = 450, Vaporize = 451, VaporizePtRadius = 452, VaporizeMarking = 453, OutStarmatikDXF = 454,
   ACBLParkPosition = 455, SafetyStopAngle = 456, CLCStrategy = 457, UseTIOWhenNoIslands = 458, KB40Type = 459,
   SheetOffsetX = 460, SheetOffsetY = 461, FoldRepFormat = 462, FoldRepAutogen = 463, LongPartsAcrossSlats = 464,
   DXFImportPtTreatment = 465, PieStrategyRange = 467, PieLength = 468, BAMethod = 469,
   BATiltAngle = 470, BATiltSpeed = 471, BAStaticAngle = 472, BAStaticSpeed = 473, BADecompression = 474,
   SlatsAlongX = 475, OffsetAlternateSlats = 476, FirstPinOffset = 477, LoadingCorner = 478, UseJawBigger = 479,
   VacuumSubgrip = 480, TrackDisplaySpan = 481, OpenContourPierce = 482, TBCPusher = 483, MaxHeadDownTraverseDist = 484,
   PointEntitiesToSkip = 485, MarkFormingFootprint = 486, PieWaitTime = 487, ACBWarnTolerance = 488, ACBErrorTolerance = 489,
   SaveToSourceDir = 490, DwgLayers = 491, FlatFormat = 492, MicrojointType = 493, NanoJointStability = 494,
   ShearedSheetMargins = 495, MicrojointSpacingOnSliceCut = 496, UseTCWRITE = 497, ErgoSide = 498, ErgoLimit = 499,
   AxisAPosZ = 500, LessNestPatterns = 501, MarkRemainderSheet = 502, CSVPathOfReleasedParts = 503, HeadUpAtEndOfPartProg = 504,
   UsingKB44 = 505, BendNCLocked = 506, DeleteOverlaps = 507, UsePieStrategy = 508, GripperToolConnection = 509,
   TextProcess = 510, FitArcOrLineThreshold = 511, SpreadOutPartsOnLayout = 512, RemoveNarrowSlits = 513, ACBCheckFrequency = 514,
   SlowerCCForConstrainedApproach = 515, KeepRAxisHighFZ38 = 516, LowerJawFZ38 = 517, KB40Frame = 518, Dynamics = 519,
   OutputGeometryInNC = 520, PreferRemSheetAlongLongDim = 521, PieRampDownLength = 522, AdditionalCenterPad = 523, ZbwBLOffset = 524,
   ZbwBROffset = 525, KB40PartAtPalletCenter = 526, KB40ModularCentralHead = 527, UseACBSpeedAsDefault = 528, GripPayloadSafetyFactor = 529,
   ShowSheetWorkingRanges = 530, FanucSubType = 531, SheetName = 532, FoldFXWithNC = 533, FoldFXDestination = 534, StartSheetSliceCutOnSheet = 535, PreferCenteringOnLSide = 536, SidesRatio = 537,
   HemDistX = 538, CFlags = 539, SuppressGasBtwnParts = 540
}

public enum EBValency {
   None = 0, Unknown = 1, Custom = 2, PTrumpf = 101, PAmada = 102, PEHT = 103, PLVDA = 104, PLVDB = 105,
   PLVDC = 106, PLVDD = 107, PGasparini = 108, PBayelerR = 109, PBayelerS = 110, PBayelerRFA = 111,
   PUrsviken = 112, PBayelerEuro = 113, PColgar = 114, PAmerican = 115, PWilaNS = 116, PKomatsu = 117,
   PWila = 118, PToyokoki = 119, PHKI = 120, PWeinbrennerS = 121, PJFY = 122, PEHT36 = 123, PEHT46 = 124,
   PTrumpfC7K = 125, PWeinbrenner50 = 126, DTrumpf = 201, DAmada30 = 202, DAmada60 = 203, DAmadaF = 204,
   DAmada2V = 205, DAmadaQuick = 206, DAmada90 = 207, DEHT = 208, DAmada120 = 209, DWeinbrenner = 210,
   DKomatsu = 211, DWila = 212, DAmadaWB = 213, DBeyeler110 = 214, DTrumpfHem = 215, DAmada160 = 216,
   DAmada190 = 217, DAmadaQuickMount = 218, DLVD = 219, DToyokokiQuickV = 220, DToyokokiQuick2V = 221,
   DAmadaQuick8 = 222, DEHT90 = 223, DUKBWB = 224, DDirect = 225, DAmada1V60 = 226, DEHT110 = 227, DEHT40 = 228,
   DTrumpfC7K = 229, DWeinbrenner50 = 230, H308K = 301, H308S = 302, H209S = 303, H391K = 304, H390K = 304,
   HZB = 306, HGaspariniR = 307, HAmadaR = 308, HWilaSmallR = 309, HU024 = 310, HWilaINZU = 901, HWilaRU = 902,
   HWila34 = 903, HWilaAmericanINZU = 1101, HWilaAmericanRU = 1102, HWilsonWTRU30 = 1304, HWilsonAmericanRU30 = 1504,
   HAmadaINZU = 1701, HAmadaRU = 1702, HAmadaRU15 = 1706, HAmadaRU20 = 1707, HBystronicBeyelerP12 = 1903,
   HBystronicBeyelerRU30 = 1904, HBystronicBeyelerRU15 = 1906, HEuramAmadaINZU = 2001, HEuramAmadaRU = 2002,
   HEuramAmadaRU30 = 2004, HEuroStampAmadaINZU = 2201, HEuroStampAmadaRU = 2202, HEuroStampAmadaRU30 = 2204,
   HEuroStampBeyelerRU = 2302, HEuroStampTrumpfINZU = 2401, HEuroStampTrumpfRU = 2402, HEuroStampTrumpfRU30 = 2404,
   HEuroStampTrumpfINBU = 2405, HFabSupplyINZU = 2501, HFabSupplyRU = 2502, HFerrariAmadaINZU = 2601, HFerrariAmadaRU = 2602,
   HFerrariAmadaRU30 = 2604, HFerrariAmadaINBU = 2605, HFerrariBeyelerRU = 2702, HFerrariTrumpfINZU = 2801,
   HFerrariTrumpfRU = 2802, HGimecAmadaINZU = 2901, HGimecAmadaRU = 2902, HGimecAmadaRU30 = 2904,
   HGimecBeyelerINZU = 3001, HGimecBeyelerRU = 3002, HGimecColgarINZU = 3101, HGimecColgarRU = 3102,
   HGimecTrumpfINZU = 3201, HGimecTrumpfRU = 3202, HMateEuropeanINZU = 3301, HMateEuropeanRU = 3302,
   HMateEuropeanRU30 = 3304, HMateWilaTrumpfINZU = 3401, HMateWilaTrumpfRU = 3402, HMateWilaTrumpfINBU = 3405,
   HRolleriAmadaINZU = 3501, HRolleriAmadaRU = 3502, HRolleriAmadaRU30 = 3504, HRolleriAmadaINBU = 3505,
   HRolleriAmadaRU15 = 3506, HRolleriAmadaRU20 = 3507, HRolleriBeyelerINZU = 3601, HRolleriBeyelerRU30 = 3604,
   HRolleriColgarRU = 3702, HRolleriGaspariniRU = 3802, HRolleriTrumpfINZU = 4001, HRolleriTrumpfRU = 4002,
   HRolleriTrumpfRU30 = 4004, HTechnoStampAmadaINZU = 4101, HTechnoStampAmadaRU = 4102, HTechnoStampAmadaINBU = 4105,
   HTechnoStampBeyelerINZU = 4201, HTechnoStampBeyelerRU = 4202, HTechnoStampBeyelerINBU = 4205, HTechnoStampTrumpfINZU = 4301,
   HTechnoStampTrumpfRU = 4302, HTechnoStampTrumpfINBU = 4305, HBarusAmadaINZU = 4701, HBarusAmadaRU = 4702,
   HBarusAmadaRU30 = 4704, HBarusAmadaRU15 = 4706, HBarusAmadaRU20 = 4707, HBarusWilaTrumpfRU30 = 4804,
   HBarusWilaTrumpfRU15 = 4806, HBarusWilaTrumpfRU20 = 4807, HToolPressBeyelerP12 = 4903, HToolPressBeyelerRU30 = 4904,
   HToolPressBeyelerRU15 = 4906, HTrumpfEHTINZU = 5001, HTrumpfEHTRU = 5002, HTrumpfEHTP12 = 5003,
   HTrumpfEHTRU30 = 5004, HTrumpfEHTINBU = 5005, HTrumpfEHTRU20 = 5007, HTrumpfEHTG25 = 5008, HUKBAmadaINZU = 5101,
   HUKBAmadaRU = 5102, HUKBAmadaRU30 = 5104, HUKBAmadaINBU = 5105, HUKBBystronicINZU = 5201, HUKBBystronicRU = 5202,
   HUKBystronicINBU = 5205, HUKBWilaINZU = 5301, HUKBWilaRU = 5302, HUKBWilaINBU = 5305, HUKBEHTINZU = 5401,
   HUKBEHTRU = 5402, HUKBEHTINBU = 5405, HUKBLVDINZU = 5501, HUKBLVDRU = 5502, HUKBLVDINBU = 5505, HUKBTrumpfINZU = 5601,
   HUKBTrumpfRU = 5602, HUKBTrumpfINBU = 5605, HUKBWeinbrennerINZU = 5701, HUKBWeinbrennerRU = 5702, HUKBWeinbrennerINBU = 5705,
   SFEV = 401, SZB = 402, STrumpf2Die = 403, SWilaINZL = 951, SWilaAmericanINZL = 1151, SAmadaINZL = 1751, SEuramAmadaINZL = 2051,
   SEuroStampAmadaINZL = 2251, SEuroStampTrumpfINZL = 2451, SFabSupplyINZL = 2551, SFerrariAmadaINZL = 2651,
   SFerrariTrumpfINZL = 2851, SGimecAmadaINZL = 2951, SGimecBeyelerINZL = 3051, SGimecColgarINZL = 3151,
   SGimecTrumpfINZL = 3251, SMateEuropeanINZL = 3351, SMateWilaTrumpfINZL = 3451, SRolleriAmadaINZL = 3551,
   SRolleriBeyelerINZL = 3651, SRolleriTrumpfINZL = 4051, STechnoStampAmadaINZL = 4151, STechnoStampBeyelerINZL = 4251,
   STechnoStampTrumpfINZL = 4351, SBarusAmadaINZL = 4751, STrumpfEHTINZL = 5051, SUKBAmadaINZL = 5151,
   SUKBBystronicINZL = 5251, SUKBWilaINZL = 5351, SUKBEHTINZL = 5451, SUKBLVDINZL = 5551, SUKBTrumpfINZL = 5651,
   SUKBWeinbrennerINZL = 5751,
}

public enum EPost {
   Free, TBC5030_B27, TBC5030_B33, TBC7030_B32, TBC7030_B34, TBC7020_B37, TBC7030_B43, TRUBEND_1100,
   TRUBEND_1066, TRUBEND_B41, TRUBEND_B42, TRUBEND_B45, TRUBEND_2100_B35, TRUBEND_3066, TRUBEND_3120,
   TRUBEND_3180, TRUBEND_3066_B26, TRUBEND_3100_B26, TRUBEND_3170_B26, TRUBEND_3066_B40, TRUBEND_3100_B40,
   TRUBEND_3170_B40, TRUBEND_3000_B38, TRUBEND_5050, TRUBEND_5085, TRUBEND_5130, TRUBEND_5170, TRUBEND_5230,
   TRUBEND_5320, TRUBEND_5085_B23, TRUBEND_5130_B23, TRUBEND_5170_B23, TRUBEND_5230_B23, TRUBEND_5320_B23,
   TRUBEND_5085_B23_BM, TRUBEND_5130_B23_BM, TRUBEND_5130_B23_BM150, TRUBEND_5320_B23_BM, TRUBEND_5320_B23_BM150,
   TRUBEND_5170_B23_BM, TRUBEND_5170_B23_BM150, TRUBEND_5230_B23_BM, TRUBEND_5230_B23_BM150, TRUMABEND_C60,
   TRUMABEND_C110, TRUMABEND_E18, TRUMABEND_E36, TRUMABEND_V50, TRUMABEND_V85, TRUMABEND_V130, TRUMABEND_V170,
   TRUMABEND_V200, TRUMABEND_V230, TRUMABEND_V320, TRUBEND_7018, TRUBEND_7036, TRUBEND_7036_B28, TRUBEND_7050_B28,
   TRUBEND_8400, TRUBEND_8230, TRUBEND_8320, TRUBEND_8500, TRUBEND_8600, TRUBEND_8800, TRUBEND_81000,
   TRUBEND_8230_B36, TRUBEND_8320_B36, TRUBEND_8400_B36, TRUBEND_8600_B36, TRUBEND_8230_TANDEM, TRUBEND_8320_TANDEM,
   TRUBEND_8230_TANDEM_B36, TRUBEND_8400_TANDEM_B36, TRUBEND_8400_TANDEM, TRUBEND_8500_TANDEM, TRUBEND_81250_TANDEM,
   VARIOPRESS_100, VARIOPRESS_130, VARIOPRESS_170, VARIOPRESS_230, VARIOPRESS_300, VARIOPRESS_400, VARIOPRESS_500,
   VARIOPRESS_600, VARIOPRESS_800, VARIOPRESS_1000, VARIOPRESS_1250, LVD_EASYFORM, MAX
}

class BendMachine {
   public int Id, MacroSizeLimit;
   public string? Name, NCName, Brand, ControlType, NCExtension, Post, BGKinematic, Flags;
   public string? EquipmentNumber;
   public EPost EPost;
   public double OpenHeight, ClosedHeight, TableLength, NCXCenter, Tonnage, BendGuardBeamWidth;
   public float DieRefHeight, CylinderGap, MinTonnageFactor;
   public EBGSystem BGSystem;
   public DRange BGMaxYDiff;
   public EBValency[]? PunchValency, DieValency;
   public BackGaugeDef[]? Gauges;
   public ACBConfig? ACB;
   public Dictionary<string, string>? Extra;
   public string[]? Materials;
   public DRange[]? ACBShadow;
   public Dictionary<SH, object>? SHDict;
   public Dictionary<string, string>? MaterialMap;
   public Dictionary<string, string>? ToolMap;
   public ClampDef? Clamp;
   public HemTableConfig? HemTable;
}

class BackGaugeDef {
   public BGSurfDef[]? Surfaces;
   public Bound3 Stroke;
   public double GapToNext;
}

abstract class BGSurfDef {
   public bool Support;
   public double DZ;
   public int UIIndex, NCIndex;
}

class FlatBGSurfDef : BGSurfDef {
   public Point3 Cen;
   public double DX;
}

class FingerBGSurfDef : BGSurfDef {
   public Point3 Cen1;
   public double Rad1, SAng1, EAng1;
   public Point2 Cen2;
   public double Rad2, SAng2, EAng2;
}

class ACBConfig {
   public float MinDistBetween, MinDistToCrowning, MaxDistCrowningToCenter;
   public short MaxDiskMeasures, MaxLaserMeasures;
}

class ClampDef {
   public int Count;
   public GeometrySource Geometry = null!;
   public double MinPitch;
   public double Pitch;
}

abstract class GeometrySource { }

class XExtrudedGeometry : GeometrySource {
   public string? File;
   public Vector3 Shift;
   public DRange[]? Spans;
}

class FileGeometry : GeometrySource {
   public string? File;
   public Vector3 Shift;
   public Quaternion Rotate;
}

public class HemTableConfig {
   public float Opening;
   public Point3 HemPoint;
   public Point3 TipPoint;
}

[AuPrimitive]
struct DRange {
   public float Min, Max;
   static DRange Read (UTFReader r) {
      r.Match ('"').Read (out float min).Match (':').Read (out float max).Match ('"');
      return new DRange { Min = min, Max = max };
   }
   void Write (UTFWriter w) => w.Write ('"').Write (Min).Write (':').Write (Max).Write ('"');
}
