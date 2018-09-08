<%@ Control Language="C#" AutoEventWireup="true" CodeFile="FirstTimeGuestEntry.ascx.cs" Inherits="RockWeb.Plugins.com_centralaz.Crm.FirstTimeGuestEntry" %>

<script type="text/javascript">
    function pageLoad()
    {
        if ($('div.alert.alert-success').length > 0)
        {
            if (document.getElementById('<%= hfShowSuccess.ClientID%>').value == "true")
            {
                window.setTimeout("fadeAndClear()", 600000);
            }
            else
            {
                $('div.alert.alert-success').animate({ opacity: 0 }, 0);
            }
        }
    }

    function fadeAndClear()
    {
        $('div.alert.alert-success').animate({ opacity: 0 }, 15000000);
        document.getElementById('<%= hfShowSuccess.ClientID%>').value = "false";
    }
</script>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <asp:HiddenField ID="hfShowSuccess" runat="server" Value="false" />
        <Rock:NotificationBox ID="nbNotice" runat="server" Visible="false" NotificationBoxType="Danger" />
        <Rock:NotificationBox ID="nbWarning" runat="server" Visible="false" NotificationBoxType="Warning" />
        <asp:Panel ID="pnlView" runat="server" CssClass="panel panel-block">

            <div class="panel-heading">
                <h1 class="panel-title"><i class="fa fa-file-o"></i> First Time Guest Entry</h1>
            </div>

            <div class="panel-body">
                <Rock:NotificationBox ID="nbMessage" runat="server" NotificationBoxType="Success" Title="Success" Visible="false" Text=""></Rock:NotificationBox>
                <asp:ValidationSummary ID="ValidationSummary1" runat="server" HeaderText="Please correct the following" CssClass="alert alert-danger" />

                <div class="row">
                    <div class="col-md-12">
                        <div class="pull-left">
                            <Rock:PersonPicker ID="ppGuest" runat="server" Label="Choose an existing person" OnSelectPerson="ppGuest_SelectPerson" />
                        </div>
                        <div class="pull-right">
                                <Rock:CampusPicker ID="cpCampus" runat="server" CssClass="input-width-lg" Label="Campus" Required="true" AutoPostBack="true" OnSelectedIndexChanged="cpCampus_SelectedIndexChanged" />
                                <Rock:RockRadioButtonList ID="rblSource" RequiredErrorMessage="An entry source is required." RepeatDirection="Vertical" runat="server" Required="true" AutoPostBack="true" OnSelectedIndexChanged="rblSource_SelectedIndexChanged"></Rock:RockRadioButtonList>
                        </div>
                    </div>
                </div>
                <asp:Panel ID="pnlNewPerson" runat="server">
                    <div class="well">
                        <p><i>Or if you can't find a person, add them as new:</i></p>
                        <div class="row">
                            <div class="col-md-6">
                                <Rock:RockTextBox ID="tbFirstName" runat="server" Label="First Name" Required="true"></Rock:RockTextBox>
                                <Rock:RockTextBox ID="tbLastName" runat="server" Label="Last Name" Required="true"></Rock:RockTextBox>
                                <asp:Panel ID="pnlCellPhone" runat="server" CssClass="row">
                                    <div class="col-sm-7">
                                        <Rock:PhoneNumberBox ID="pnCell" runat="server" Label="Cell Phone" />
                                    </div>
                                    <div class="col-sm-5">
                                        <Rock:RockCheckBox ID="cbSms" runat="server" Label="&nbsp;" Text="Enable SMS" />
                                    </div>
                                </asp:Panel>
                                <Rock:EmailBox ID="tbEmail" runat="server" Label="Email" Required="false"></Rock:EmailBox>
                                <asp:Panel ID="pnlHomePhone" runat="server" CssClass="row">
                                    <div class="col-sm-7">
                                        <Rock:PhoneNumberBox ID="pnHome" runat="server" Label="Home Phone" />
                                    </div>
                                    <div class="col-sm-5">
                                    </div>
                                </asp:Panel>
                                <Rock:AddressControl ID="acAddress" Label="Address" runat="server" />
                            </div>
                            <div class="col-md-6">
                                <Rock:RockTextBox ID="tbSpouseFirstName" runat="server" Label="Spouse First Name"></Rock:RockTextBox>
                                <Rock:RockTextBox ID="tbSpouseLastName" runat="server" Label="Spouse Last Name"></Rock:RockTextBox>
                                <div class="row">
                                    <div class="col-sm-7">
                                        <Rock:PhoneNumberBox ID="pnSpouseCell" runat="server" Label="Spouse Cell Phone" />
                                    </div>
                                    <div class="col-sm-5">
                                        <Rock:RockCheckBox ID="cbSpouseSms" runat="server" Label="&nbsp;" Text="Enable SMS" />
                                    </div>
                                </div>
                                <Rock:EmailBox ID="tbSpouseEmail" runat="server" Label="Email" />

                            </div>
                        </div>
                    </div>
                </asp:Panel>

                <Rock:RockCheckBoxList ID="cblInterests" runat="server" Label="Interested in" />
                <Rock:RockTextBox ID="tbComments" Label="Comments" runat="server" TextMode="MultiLine" Rows="3" />
                <Rock:RockTextBox ID="tbPrayerRequests" Label="Prayer Requests" runat="server" TextMode="MultiLine" Rows="2" />

                <div class="actions">
                    <asp:LinkButton ID="btnSave" runat="server" CssClass="btn btn-primary margin-r-lg" Text="Save" OnClick="btnSave_Click" />
                    <asp:LinkButton ID="btnCancel" runat="server" Text="Done" CausesValidation="false" OnClick="btnCancel_Click" />
                </div>
            </div>

        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>
