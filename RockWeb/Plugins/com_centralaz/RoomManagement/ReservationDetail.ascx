﻿<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ReservationDetail.ascx.cs" Inherits="RockWeb.Plugins.com_centralaz.RoomManagement.ReservationDetail" %>
<%@ Register TagPrefix="CentralAZ" Assembly="com.centralaz.RoomManagement" Namespace="com.centralaz.RoomManagement.Web.UI.Controls" %>
<script type="text/javascript">
    function clearActiveDialog() {
        $('#<%=hfActiveDialog.ClientID %>').val('');
    }
</script>
<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <Rock:NotificationBox ID="nbErrorWarning" runat="server" NotificationBoxType="Danger" />
        <asp:Panel ID="pnlView" runat="server" CssClass="panel panel-block">

            <div class="panel-heading">
                <h1 class="panel-title">
                    <asp:Literal ID="lPanelTitle" runat="server" /></h1>
            </div>
            <div class="panel-body">
                <div class="row">
                    <div class="col-md-6">
                        <Rock:RockTextBox ID="rtbName" runat="server" Label="Event Name" Required="true" />
                        <Rock:RockDropDownList ID="ddlCampus" runat="server" Label="Campus" Required="false" />
                        <Rock:RockDropDownList ID="ddlMinistry" runat="server" Label="Ministry" Required="false" />
                        <Rock:RockTextBox ID="rtbNote" runat="server" Label="Notes" TextMode="MultiLine" />
                        <div class="row">
                            <div class="col-md-6">
                                <Rock:NumberBox ID="nbAttending" runat="server" NumberType="Integer" MinimumValue="0" Label="Number Attending" Required="false" />
                            </div>
                            <div class="col-md-6">
                                <div class="form-group" id="divStatus" runat="server">
                                    <div class="form-control-static">
                                        <asp:HiddenField ID="hfApprovalState" runat="server" OnValueChanged="hfApprovalState_ValueChanged" />
                                        <asp:Panel ID="pnlEditApprovalState" runat="server" Visible="false">
                                            <label class="control-label">Status</label>

                                            <div class="toggle-container">
                                                <div class="btn-group btn-toggle">
                                                    <a class="btn btn-xs <%=PendingCss%>" data-status="1" data-active-css="btn-warning">Unapproved</a>
                                                    <a class="btn btn-xs <%=ApprovedCss%>" data-status="2" data-active-css="btn-success">Approved</a>
                                                    <a class="btn btn-xs <%=DeniedCss%>" data-status="3" data-active-css="btn-danger">Denied</a>
                                                </div>
                                            </div>
                                        </asp:Panel>
                                        <asp:Panel ID="pnlReadApprovalState" runat="server" Visible="false">
                                            <label class="control-label">Status</label>
                                            <asp:Literal ID="lApprovalState" runat="server" />
                                        </asp:Panel>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="col-md-6">
                        <div class="row">
                            <div class="col-md-6">
                                <Rock:RockControlWrapper ID="rcwSchedule" runat="server" Label="Schedule">
                                    <Rock:ScheduleBuilder ID="sbSchedule" runat="server" ValidationGroup="Schedule" Required="true" OnSaveSchedule="sbSchedule_SaveSchedule" />
                                    <asp:Literal ID="lScheduleText" runat="server" />
                                </Rock:RockControlWrapper>
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-md-6">
                                <Rock:NumberBox ID="nbSetupTime" runat="server" NumberType="Integer" MinimumValue="0" Label="Setup Time" Required="false" OnTextChanged="nbSetupTime_TextChanged" Help="The number of minutes it will take to set up the event." />
                            </div>
                            <div class="col-md-6">
                                <Rock:NumberBox ID="nbCleanupTime" runat="server" NumberType="Integer" MinimumValue="0" Label="Cleanup Time" Required="false" OnTextChanged="nbCleanupTime_TextChanged" Help="The number of minutes it will take to clean up the event." />
                            </div>
                        </div>
                        <Rock:PanelWidget ID="wpLocations" runat="server" Title="Locations">
                            <div class="grid">
                                <Rock:ModalAlert ID="maLocationGridWarning" runat="server" />
                                <Rock:Grid ID="gLocations" runat="server" AllowPaging="false" DisplayType="Light" RowItemText="Location" ShowConfirmDeleteDialog="false" OnRowDataBound="gLocations_RowDataBound">
                                    <Columns>
                                        <Rock:RockBoundField DataField="Location" HeaderText="Location" />
                                        <Rock:RockBoundField DataField="ApprovalState" HeaderText="Approved?" />
                                        <Rock:LinkButtonField CssClass="btn btn-success btn-sm" OnClick="gLocations_ApproveClick" Text="Approve" Visible="true" />
                                        <Rock:LinkButtonField CssClass="btn btn-danger btn-sm" OnClick="gLocations_DenyClick" Text="Deny" Visible="true" />
                                        <Rock:EditField OnClick="gLocations_Edit" />
                                        <Rock:DeleteField OnClick="gLocations_Delete" />
                                    </Columns>
                                </Rock:Grid>
                            </div>
                        </Rock:PanelWidget>
                        <Rock:PanelWidget ID="wpResources" runat="server" Title="Resources">
                            <div class="grid">
                                <Rock:ModalAlert ID="maResourceGridWarning" runat="server" />
                                <Rock:Grid ID="gResources" runat="server" AllowPaging="false" DisplayType="Light" RowItemText="Resource" ShowConfirmDeleteDialog="false" OnRowDataBound="gResources_RowDataBound">
                                    <Columns>
                                        <Rock:RockBoundField DataField="Resource.Name" HeaderText="Resource" />
                                        <Rock:RockBoundField DataField="Quantity" HeaderText="Quantity" />
                                        <Rock:RockBoundField DataField="ApprovalState" HeaderText="Approved?" />
                                        <Rock:LinkButtonField CssClass="btn btn-success" OnClick="gResources_ApproveClick" Text="Approve" Visible="true" />
                                        <Rock:LinkButtonField CssClass="btn btn-danger" OnClick="gResources_DenyClick" Text="Deny" Visible="true" />
                                        <Rock:EditField OnClick="gResources_Edit" />
                                        <Rock:DeleteField OnClick="gResources_Delete" />
                                    </Columns>
                                </Rock:Grid>
                            </div>
                        </Rock:PanelWidget>
                    </div>
                </div>
                <div class="actions">
                    <asp:LinkButton ID="btnSave" runat="server" Text="Save" CssClass="btn btn-primary" OnClick="btnSave_OnClick" />
                    <asp:LinkButton ID="btnCancel" runat="server" Text="Cancel" CssClass="btn btn-link" OnClick="btnCancel_OnClick" CausesValidation="false" />
                </div>
            </div>
        </asp:Panel>

        <asp:HiddenField ID="hfActiveDialog" runat="server" />

        <Rock:ModalDialog ID="dlgReservationLocation" runat="server" Title="Select Location" OnSaveClick="dlgReservationLocation_SaveClick" OnCancelScript="clearActiveDialog();" ValidationGroup="ReservationLocation">
            <Content>
                <asp:HiddenField ID="hfAddReservationLocationGuid" runat="server" />
                <asp:ValidationSummary ID="valReservationLocationSummary" runat="server" HeaderText="Please Correct the Following" CssClass="alert alert-danger" ValidationGroup="ReservationLocation" />
                <div class="row">
                    <div class="col-md-6">
                        <CentralAZ:ScheduledLocationItemPicker ID="slpLocation" runat="server" Label="Location" Required="false" Enabled="false" AllowMultiSelect="false" OnSelectItem="slpLocation_SelectItem" ValidationGroup="ReservationLocation" />
                    </div>
                </div>
            </Content>
        </Rock:ModalDialog>

        <Rock:ModalDialog ID="dlgReservationResource" runat="server" Title="Select Resource" OnSaveClick="dlgReservationResource_SaveClick" OnCancelScript="clearActiveDialog();" ValidationGroup="ReservationResource">
            <Content>
                <asp:HiddenField ID="hfAddReservationResourceGuid" runat="server" />
                <asp:ValidationSummary ID="valReservationResourceSummary" runat="server" HeaderText="Please Correct the Following" CssClass="alert alert-danger" ValidationGroup="ReservationResource" />
                <div class="row">
                    <div class="col-md-6">
                        <CentralAZ:ScheduledResourcePicker ID="srpResource" runat="server" Label="Resource" Required="false" Enabled="false" AllowMultiSelect="false" OnSelectItem="srpResource_SelectItem" ValidationGroup="ReservationResource" />
                    </div>
                    <div class="col-md-6">
                        <Rock:NumberBox ID="nbQuantity" runat="server" NumberType="Integer" MinimumValue="1" ValidationGroup="ReservationResource" Label="Quantity" />
                    </div>
                </div>
            </Content>
        </Rock:ModalDialog>
    </ContentTemplate>
</asp:UpdatePanel>