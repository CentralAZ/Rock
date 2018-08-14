<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ImportEvents.ascx.cs" Inherits="RockWeb.Plugins.com_centralaz.Event.ImportEvents" %>

<asp:UpdatePanel ID="upnlExample" runat="server">
    <ContentTemplate>
        <asp:HiddenField ID="hfFileEncoding" runat="server" />
        <div class="panel panel-block">
            <div class="panel panel-heading">
                Import Events
                <small class="text-muted pull-right">v<asp:Literal ID="lVersionText" runat ="server"></asp:Literal></small>
            </div>
            <div class="panel panel-body">
                <div class="alert alert-danger" runat="server" id="divErrors" visible="false">
                        <b>There was a problem with the file you are trying to upload.</b>
                        <div>
                            <asp:Label ID="lblErrors" runat="server" ></asp:Label>
                        </div>
                </div>
                <asp:Wizard ID="Wizard1" runat="server" ActiveStepIndex="0"
                    Width="100%" 
                    OnFinishButtonClick="Wizard1_FinishButtonClick"
                    StartNextButtonStyle-CssClass="btn btn-default"
                    FinishCompleteButtonStyle-CssClass="btn btn-primary margin-l-md"
                    StepNextButtonStyle-CssClass="btn btn-default  margin-l-md"
                    StepPreviousButtonStyle-CssClass="btn btn-default margin-r-md" 
                    SideBarStyle-VerticalAlign="Top"
                    SideBarStyle-Width="10%"
                    SideBarButtonStyle-CssClass="btn btn-default" 
                    SideBarButtonStyle-Width="100%"
                    FinishPreviousButtonStyle-CssClass="btn btn-default" 
                    SideBarStyle-CssClass="padding-all-md  well" SideBarStyle-BackColor="#dddddd"
                    NavigationStyle-CssClass="padding-all-md well"
                    StepStyle-CssClass="alert "
                    OnNextButtonClick="Wizard1_NextButtonClick">
                    <WizardSteps>
                        <asp:WizardStep ID="WizardStep1" runat="server" Title="Upload List Data" >
                            <div class="col-md-12">

                                <h3>Step 1 - Choose File to Import</h3>
                                Upload a file (csv) containing the following nine (9) required fields:<br />
                                <asp:BulletedList ID="BulletedList1" runat="server">
                                    <asp:ListItem>Name</asp:ListItem>
                                    <asp:ListItem>CrossStreets</asp:ListItem>
                                    <asp:ListItem>StreetAddress</asp:ListItem>
                                    <asp:ListItem>City</asp:ListItem>
                                    <asp:ListItem>State</asp:ListItem>
                                    <asp:ListItem>Zip</asp:ListItem>
                                    <asp:ListItem>Campus</asp:ListItem>
                                    <asp:ListItem>MaxRegistrants</asp:ListItem>
                                    <asp:ListItem>Description</asp:ListItem>
                                </asp:BulletedList>
                                <div class="row">
                                    <div class="col-md-12">
                                        <Rock:FileUploader ID="fuprExampleContentFile" runat="server" Label="CSV File" Required="true" IsBinaryFile="false" RootFolder="~/App_Data/TemporaryFiles" OnFileUploaded="fupContentFile_FileUploaded" />
<%--                                        <asp:FileUpload ID="fuUpload" runat="server" CssClass="btn btn-primary" />
                                        <asp:RequiredFieldValidator ID="RequiredFieldValidator1" runat="server"
                                            ControlToValidate="fuUpload" CssClass="text-danger" ErrorMessage="Please select a file to upload">*</asp:RequiredFieldValidator>--%>
                                    </div>
                                </div>

                            </div>
                        </asp:WizardStep>

                        <asp:WizardStep ID="WizardStep2" runat="server" Title="Set Options">
                            <div class="col-md-12">
                                <h3>Step  2 - Set Options</h3>
                                <div class="pull-right">
                                    <asp:LinkButton ID="btnClearSettings" runat="server" CssClass="btn btn-default btn-xs" OnClick="btnClearSettings_Click" Text="Clear Settings" CausesValidation="false"></asp:LinkButton>
                                </div>
                                <p>
                                    Specify the options that should be used during the import:
                                </p>
                                <br />
                                <Rock:RockDropDownList ID="ddlTemplates" DataTextField="Name" DataValueField="Id" runat="server" Required="true"
                                    Label="Registration Template" OnSelectedIndexChanged="ddlTemplates_SelectedIndexChanged"
                                     AutoPostBack="true" Help="The template to use for setting up the event registrations." />
                                <Rock:DataDropDownList ID="ddlRegistrationInstanceSource" runat="server" EnhanceForLongLists="false"  Enabled="false"
                                    Label="Registration Instance Source (to copy other details from)" 
                                    Help="This is the source instance that will be used to copy other details from when creating or updating new registration instances." 
                                    SourceTypeName="Rock.Model.RegistrationInstance, Rock" DataTextField="Name" DataValueField="Id" PropertyName="Id"
                                    OnSelectedIndexChanged="ddlRegistrationInstanceSource_SelectedIndexChanged" 
                                    AutoPostBack="true" CausesValidation="true" Required="true" />
                                <Rock:RockDropDownList ID="ddlEventItems" DataTextField="Name" DataValueField="Id" runat="server" Required="true"
                                    Label="Event Items" OnSelectedIndexChanged="ddlEventItems_SelectedIndexChanged"
                                     AutoPostBack="true" Help="The event item to add the occurrences to when creating the events." />
                                <Rock:GroupTypePicker ID="gtpGroupType" runat="server" Label="Group Type" OnDataBound="gtpGroupType_DataBound" OnSelectedIndexChanged="gtpGroupType_SelectedIndexChanged" AutoPostBack="true" Required="true"></Rock:GroupTypePicker>
                                <Rock:GroupPicker ID="gpParentGroup" runat="server" Label="Parent Group (to add the groups under)" Enabled="false" Required="true" />

                                <h4>Event Date, Time and Duration</h4>
                                <Rock:RockControlWrapper ID="rcwSchedule" runat="server" Label="Schedule" >
                                    <Rock:ScheduleBuilder ID="sbSchedule" runat="server" ValidationGroup="Schedule" AllowMultiSelect="true" Required="true" OnSaveSchedule="sbSchedule_SaveSchedule"/>
                                    <asp:Literal ID="lScheduleText" runat="server" />
                                </Rock:RockControlWrapper>
                            </div>
                        </asp:WizardStep>

                        <asp:WizardStep runat="server" StepType="Finish" Title="Summary">
                            <div class="col-md-12">
                                <h3>Summary</h3>
                                <br />
                                <asp:Label ID="lblSummary" runat="server"></asp:Label>
                                <br />
                                <div class="alert alert-info">Press Finish to complete the import.</div>
                            </div>
                        </asp:WizardStep>

                        <asp:WizardStep runat="server" StepType="Complete" Title="Complete">
                            <div class="col-md-12">
                                <h3>Complete</h3>
                                <br />
                                <asp:Label ID="lblComplete" runat="server"
                                    Text="Finished!"></asp:Label>
                                <br />
                                <br />
                                <asp:Label ID="lblCompleteMsg" runat="server"></asp:Label>
                            </div>
                        </asp:WizardStep>

                    </WizardSteps>

                </asp:Wizard>

            </div>
        </div>
    </ContentTemplate>
</asp:UpdatePanel>
