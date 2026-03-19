//_______________________Migration_Rule________________________________
//  1. Step names (s.name) should not have duplicates.
//  2. Page names and page results must match:
//      - assume  page name (p.name) is `PageName`
//      - then survey name must be `pageNameSurvey` first letter is lowercase and add Survey at the end.
//_____________________________________________________________________
export interface BaseStepPgTypes {
  _StepNo: number;
  [key: string]: number;
}
export interface stepsAndPagesNumberInfoType {
  DECEASED: DECEASEDstPgTypes;
  WILL: WILLstPgTypes;
  RELATIONS: RELATIONSstPgTypes;
  APPLICANT: APPLICANTstPgTypes;
  NOTIFY: NOTIFYstPgTypes;
  BELONGINGS: BELONGINGSstPgTypes;
  NOWILL: NOWILLstPgTypes;
  OVERVIEW: OVERVIEWstPgTypes;
  REVIEW: REVIEWstPgTypes;
  SUBMIT: SUBMITstPgTypes;
  NEXT: NEXTstPgTypes;
}

export interface DECEASEDstPgTypes {
  _StepNo: number;
  InformationAboutDeceased: number;
}

export interface WILLstPgTypes {
  _StepNo: number;
  DeceasedWill: number;
}

export interface RELATIONSstPgTypes {
  _StepNo: number;
  Spouse: number;
  Children: number;
  Parents: number;
  GrandChildren: number;
  GreatGrandChildren: number;
  Creditors: number;
}

export interface APPLICANTstPgTypes {
  _StepNo: number;
  ApplicantInfo: number;
  ApplicantCitor: number;
  ApplicantService: number;
}

export interface NOTIFYstPgTypes {
  _StepNo: number;
  ReviewYourAnswers: number;
  TellPeople: number;
  PreviewP1: number;
  PreviewPGT: number;
  NotifyPeople: number;
  PreviewP9: number;
}

export interface BELONGINGSstPgTypes {
  _StepNo: number;
  LandBuildings: number;
  Vehicles: number;
  BankAccounts: number;
  PensionsInsurance: number;
  PersonalItems: number;
}

export interface NOWILLstPgTypes {
  _StepNo: number;
  NoWillNotification: number;
}

export interface OVERVIEWstPgTypes {
  _StepNo: number;
  WillSearchCheck: number;
  FinalizeAssetValues: number;
}

export interface REVIEWstPgTypes {
  _StepNo: number;
  ReviewYourAnswers: number;
}

export interface SUBMITstPgTypes {
  _StepNo: number;
  FilingOptions: number;
  ReviewAndPrint: number;
  ReviewAndSave: number;
  ReviewAndSubmit: number;
  NextSteps: number;
}

export interface NEXTstPgTypes extends BaseStepPgTypes {
  _StepNo: number;
  FormP5: number;
  FormP10: number;
  NoticeWillSearch: number;
  FormP2: number;
}
