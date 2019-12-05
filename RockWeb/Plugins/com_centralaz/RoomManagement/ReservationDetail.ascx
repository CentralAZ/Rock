<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ReservationDetail.ascx.cs" Inherits="RockWeb.Plugins.com_centralaz.RoomManagement.ReservationDetail" %>
<%@ Register TagPrefix="CentralAZ" Assembly="com.centralaz.RoomManagement" Namespace="com.centralaz.RoomManagement.Web.UI.Controls" %>
<script type="text/javascript">
    function clearActiveDialog() {
        $('#<%=hfActiveDialog.ClientID %>').val('');
    }
</script>
<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <asp:Panel ID="pnlDetails" runat="server" CssClass="panel panel-block">

            <asp:HiddenField ID="hfReservationId" runat="server" />

            <div class="panel-heading">
                <h1 class="panel-title">
                    <asp:Literal ID="lPanelTitle" runat="server" />
                </h1>
                <div class="panel-labels">
                    <Rock:HighlightLabel ID="hlStatus" runat="server" />
                </div>
            </div>

            <Rock:PanelDrawer ID="pdAuditDetails" runat="server"></Rock:PanelDrawer>

            <div class="panel-body">
                <Rock:NotificationBox ID="nbEditModeMessage" runat="server" NotificationBoxType="Info" />

                <asp:ValidationSummary ID="ValidationSummary1" runat="server" HeaderText="Please Correct the Following" CssClass="alert alert-danger" />
                <Rock:NotificationBox ID="nbError" runat="server" NotificationBoxType="Danger" />
                <Rock:NotificationBox ID="nbWarning" runat="server" NotificationBoxType="Warning" />

                <asp:Panel ID="pnlViewDetails" runat="server">

                    <div class="row">
                        <div class="col-md-6">
                            <div class="row">
                                <div class="col-md-6">
                                    <Rock:RockLiteral ID="lName" runat="server" Label="Name" />
                                </div>
                                <div class="col-md-6">
                                    <Rock:RockLiteral ID="lReservationType" runat="server" Label="Reservation Type" />
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-6">
                                    <Rock:RockLiteral ID="lSchedule" runat="server" Label="Schedule" />
                                </div>
                                <div class="col-md-3">
                                    <Rock:RockLiteral ID="lSetupTime" runat="server" Label="Setup Time" />
                                </div>
                                <div class="col-md-3">
                                    <Rock:RockLiteral ID="lCleanupTime" runat="server" Label="Cleanup Time" />
                                </div>
                            </div>

                            <div class="row">
                                <div class="col-md-6">
                                    <Rock:RockLiteral ID="lCampus" runat="server" Label="Campus" />
                                </div>
                                <div class="col-md-6">
                                    <Rock:RockLiteral ID="lMinistry" runat="server" Label="Ministry" />
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <Rock:RockLiteral ID="lNumberAttending" runat="server" Label="Number Attending" />
                                </div>
                                <div class="col-md-6">
                                    <Rock:RockLiteral ID="lSetupPhoto" runat="server" Label="Setup Photo" />
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <Rock:RockLiteral ID="lEventContact" runat="server" Label="Event Contact" />
                                </div>
                                <div class="col-md-6">
                                    <Rock:RockLiteral ID="lAdminContact" runat="server" Label="Administrative Contact" />
                                </div>
                            </div>
                            <Rock:RockLiteral ID="lNotes" runat="server" Label="Notes" />
                            <div class="row">
                                <div class="col-md-6">
                                    <Rock:RockLiteral ID="lApproval" runat="server" Label="Approval State" />
                                </div>
                            </div>
                        </div>
                        <div class="col-md-6">

                            <div class="grid">
                                <label class="control-label">Locations</label>
                                <Rock:Grid ID="gViewLocations" runat="server" AllowPaging="false" DisplayType="Light" RowItemText="Location" OnRowDataBound="gViewLocations_RowDataBound">
                                    <Columns>
                                        <Rock:RockBoundField DataField="Location" HeaderText="Location" />
                                        <Rock:RockBoundField DataField="LocationLayout.Name" HeaderText="Layout" />
                                        <Rock:RockBoundField DataField="LocationLayout.Description" HeaderText="Description" />
                                        <Rock:RockTemplateField HeaderText="Photo">
                                            <ItemTemplate>
                                                <asp:Literal ID="lLayoutPhoto" runat="server" />
                                            </ItemTemplate>
                                        </Rock:RockTemplateField>
                                        <Rock:RockBoundField DataField="ApprovalState" HeaderText="Approved?" />
                                        <Rock:LinkButtonField CssClass="btn btn-success btn-sm" OnClick="gViewLocations_ApproveClick" Text="<i class='fa fa-check'></i>" Visible="true" />
                                        <Rock:LinkButtonField CssClass="btn btn-danger btn-sm" OnClick="gViewLocations_DenyClick" Text="<i class='fa fa-ban'></i>" Visible="true" />
                                    </Columns>
                                </Rock:Grid>
                            </div>

                            <div class="grid">
                                <label class="control-label">Resources</label>
                                <Rock:Grid ID="gViewResources" runat="server" AllowPaging="false" DisplayType="Light" RowItemText="Resource" OnRowDataBound="gViewResources_RowDataBound">
                                    <Columns>
                                        <Rock:RockBoundField DataField="Resource.Name" HeaderText="Resource" />
                                        <Rock:RockTemplateField>
                                            <ItemTemplate><em class="text-muted"><%# Convert.ToString( Eval( "Resource.Location.Name") ) == string.Empty ? "" : "(attached to " +  Eval("Resource.Location.Name") + ")" %></em></ItemTemplate>
                                        </Rock:RockTemplateField>
                                        <Rock:RockBoundField DataField="Quantity" HeaderText="Qty" />
                                        <Rock:RockBoundField DataField="ApprovalState" HeaderText="Approved?" />
                                        <Rock:LinkButtonField CssClass="btn btn-sm btn-success" OnClick="gViewResources_ApproveClick" Text="<i class='fa fa-check'></i>" Visible="true" />
                                        <Rock:LinkButtonField CssClass="btn btn-sm btn-danger" OnClick="gViewResources_DenyClick" Text="<i class='fa fa-ban'></i>" Visible="true" />
                                    </Columns>
                                </Rock:Grid>
                            </div>
                        </div>
                    </div>

                    <asp:PlaceHolder ID="phAttributes" runat="server"></asp:PlaceHolder>
                    <asp:PlaceHolder ID="phViewLocationAnswers" runat="server" EnableViewState="false" />
                    <asp:PlaceHolder ID="phViewResourceAnswers" runat="server" EnableViewState="false" />

                    <div class="row">
                        <div class="col-md-6">
                            <Rock:ModalAlert ID="mdWorkflowLaunched" runat="server" />
                            <asp:Label ID="lblWorkflows" Text="Available Workflows" Font-Bold="true" runat="server" />
                            <div class="margin-b-md">
                                <asp:Repeater ID="rptWorkflows" runat="server">
                                    <ItemTemplate>
                                        <asp:LinkButton ID="lbWorkflow" runat="server" CssClass="btn btn-default btn-xs" CommandArgument='<%# Eval("Id") %>' CommandName="LaunchWorkflow">
                                        <%# Eval("WorkflowType.Name") %>
                                        </asp:LinkButton>
                                    </ItemTemplate>
                                </asp:Repeater>
                            </div>
                        </div>
                    </div>
                    <div class="actions">
                        <asp:LinkButton ID="btnEdit" runat="server" AccessKey="e" ToolTip="Alt+e" Text="Edit" CssClass="btn btn-primary" OnClick="btnEdit_OnClick" CausesValidation="false" />
                        <asp:LinkButton ID="btnDelete" runat="server" Text="Delete" CssClass="btn btn-link" OnClick="btnDelete_OnClick" CausesValidation="false" />
                        <div class="pull-right">
                            <asp:LinkButton ID="btnApprove" runat="server" ToolTip="Approve Reservation" CssClass="btn btn-success" OnClick="btnApprove_Click" CausesValidation="false">Approve</asp:LinkButton>
                            <asp:LinkButton ID="btnDeny" runat="server" ToolTip="Approve Reservation" CssClass="btn btn-danger" OnClick="btnDeny_Click" CausesValidation="false">Deny</asp:LinkButton>
                            <asp:LinkButton ID="btnOverride" runat="server" ToolTip="Override Reservation" CssClass="btn btn-warning" OnClick="btnOverride_Click" CausesValidation="false">Override</asp:LinkButton>
                            <asp:LinkButton ID="btnCopy" runat="server" ToolTip="Copy Reservation" CssClass="btn btn-default btn-sm fa fa-clone" OnClick="btnCopy_Click" CausesValidation="false" />
                        </div>
                    </div>


                </asp:Panel>

                <asp:Panel ID="pnlEditDetails" runat="server">

                    <div class="row">
                        <div class="col-md-6">
                            <div class="row">
                                <div class="col-md-6">
                                    <Rock:DataTextBox ID="rtbName" runat="server" Label="Event Name" Required="true" SourceTypeName="com.centralaz.RoomManagement.Model.Reservation, com.centralaz.RoomManagement" PropertyName="Name" />
                                </div>
                                <div class="col-md-6">
                                    <Rock:RockDropDownList ID="ddlReservationType" Label="Reservation Type" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlReservationType_SelectedIndexChanged" />
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <Rock:RockControlWrapper ID="rcwSchedule" runat="server" Label="Schedule">
                                        <Rock:ScheduleBuilder ID="sbSchedule" runat="server" ValidationGroup="Schedule" Required="true" OnSaveSchedule="sbSchedule_SaveSchedule" />
                                        <asp:Literal ID="lScheduleText" runat="server" />
                                    </Rock:RockControlWrapper>
                                </div>
                                <div class="col-md-3">
                                    <Rock:NumberBox ID="nbSetupTime" runat="server" NumberType="Integer" MinimumValue="0" Label="How many minutes to set up?" OnTextChanged="nbSetupTime_TextChanged" Help="The number of minutes it will take to set up the event." RequiredErrorMessage="You must supply a number for setup time (even if 0 minutes) as this will effect when others can reserve the same location/resource." />
                                </div>
                                <div class="col-md-3">
                                    <Rock:NumberBox ID="nbCleanupTime" runat="server" NumberType="Integer" MinimumValue="0" Label="How many minutes to clean up?" OnTextChanged="nbCleanupTime_TextChanged" Help="The number of minutes it will take to clean up the event." RequiredErrorMessage="You must supply a number for cleanup time (even if 0 minutes) as this will effect when others can reserve the same location/resource." />
                                </div>
                            </div>

                            <Rock:RockDropDownList ID="ddlCampus" runat="server" Label="Campus" Required="false" OnSelectedIndexChanged="ddlCampus_SelectedIndexChanged" AutoPostBack="true" />
                            <Rock:RockDropDownList ID="ddlMinistry" runat="server" Label="Ministry" Required="false" />

                            <div class="row">
                                <div class="col-md-6">
                                    <Rock:NumberBox ID="nbAttending" runat="server" NumberType="Integer" MinimumValue="0" Label="Number Attending" Required="false" />
                                </div>
                                <div class="col-md-6">
                                    <Rock:FileUploader ID="fuSetupPhoto" runat="server" Label="Setup Photo" Help="If you'd like a special setup for your event, please upload a photo or diagram here." />
                                </div>
                            </div>
                            <div class="row">
                                <div class="col-md-6">
                                    <Rock:PersonPicker ID="ppEventContact" runat="server" Label="Event Contact" EnableSelfSelection="true" OnSelectPerson="ppEventContact_SelectPerson" Help="The person who will be on-site to manage this reservation." />
                                    <Rock:PhoneNumberBox ID="pnEventContactPhone" runat="server" Label="Event Contact Phone" />
                                    <Rock:EmailBox ID="tbEventContactEmail" runat="server" Label="Event Contact Email" />
                                </div>
                                <div class="col-md-6">
                                    <Rock:PersonPicker ID="ppAdministrativeContact" runat="server" Label="Administrative Contact" EnableSelfSelection="true" OnSelectPerson="ppAdministrativeContact_SelectPerson" Help="The person who set up this reservation." />
                                    <Rock:PhoneNumberBox ID="pnAdministrativeContactPhone" runat="server" Label="Administrative Contact Phone" />
                                    <Rock:EmailBox ID="tbAdministrativeContactEmail" runat="server" Label="Administrative Contact Email" />
                                </div>
                            </div>
                            <Rock:DataTextBox ID="rtbNote" runat="server" Label="Notes" TextMode="MultiLine" Rows="4" MaxLength="2500" SourceTypeName="com.centralaz.RoomManagement.Model.Reservation, com.centralaz.RoomManagement" PropertyName="Note" />

                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-group" id="divStatus" runat="server">
                                        <div class="form-control-static">
                                            <asp:HiddenField ID="hfApprovalState" runat="server" OnValueChanged="hfApprovalState_ValueChanged" />
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
                            <Rock:PanelWidget ID="wpLocations" runat="server" Title="Locations">
                                <div class="grid">
                                    <Rock:ModalAlert ID="maLocationGridWarning" runat="server" />
                                    <Rock:Grid ID="gLocations" runat="server" AllowPaging="false" DisplayType="Light" RowItemText="Location" ShowConfirmDeleteDialog="false">
                                        <Columns>
                                            <Rock:RockBoundField DataField="Location" HeaderText="Location" />
                                            <Rock:RockBoundField DataField="LocationLayout.Name" HeaderText="Layout" />
                                            <Rock:RockBoundField DataField="ApprovalState" HeaderText="Approved?" />
                                            <Rock:EditField OnClick="gLocations_Edit" />
                                            <Rock:DeleteField OnClick="gLocations_Delete" />
                                        </Columns>
                                    </Rock:Grid>
                                </div>
                            </Rock:PanelWidget>

                            <Rock:PanelWidget ID="wpResources" runat="server" Title="Resources">
                                <div class="grid">
                                    <Rock:ModalAlert ID="maResourceGridWarning" runat="server" />
                                    <Rock:Grid ID="gResources" runat="server" AllowPaging="false" DisplayType="Light" RowItemText="Resource" ShowConfirmDeleteDialog="false">
                                        <Columns>
                                            <Rock:RockBoundField DataField="Resource.Name" HeaderText="Resource" />
                                            <Rock:RockTemplateField>
                                                <ItemTemplate><em class="text-muted"><%# Convert.ToString( Eval( "Resource.Location.Name") ) == string.Empty ? "" : "(attached to " +  Eval("Resource.Location.Name") + ")" %></em></ItemTemplate>
                                            </Rock:RockTemplateField>
                                            <Rock:RockBoundField DataField="Quantity" HeaderText="Qty" />
                                            <Rock:RockBoundField DataField="ApprovalState" HeaderText="Approved?" />
                                            <Rock:EditField OnClick="gResources_Edit" />
                                            <Rock:DeleteField OnClick="gResources_Delete" />
                                        </Columns>
                                    </Rock:Grid>
                                </div>
                            </Rock:PanelWidget>
                        </div>
                    </div>
                    <Rock:DynamicPlaceholder ID="phAttributeEdits" runat="server" />
                    <asp:PlaceHolder ID="phLocationAnswers" runat="server" EnableViewState="false" />
                    <asp:PlaceHolder ID="phResourceAnswers" runat="server" EnableViewState="false" />
                    <div class="actions">
                        <asp:LinkButton ID="btnSave" runat="server" Text="Save" CssClass="btn btn-primary" OnClick="btnSave_OnClick" />
                        <asp:LinkButton ID="btnCancel" runat="server" Text="Cancel" CssClass="btn btn-link" OnClick="btnCancel_OnClick" CausesValidation="false" />
                    </div>
                </asp:Panel>
            </div>
        </asp:Panel>

        <asp:HiddenField ID="hfActiveDialog" runat="server" />

        <Rock:ModalDialog ID="dlgReservationLocation" runat="server" Title="Select Location" OnSaveThenAddClick="dlgReservationLocation_SaveThenAddClick" OnSaveClick="dlgReservationLocation_SaveClick" OnCancelScript="clearActiveDialog();" ValidationGroup="ReservationLocation">
            <Content>
                <asp:HiddenField ID="hfAddReservationLocationGuid" runat="server" />
                <asp:ValidationSummary ID="valReservationLocationSummary" runat="server" HeaderText="Please Correct the Following" CssClass="alert alert-danger" ValidationGroup="ReservationLocation" />
                <div class="row">
                    <div class="col-md-6">
                        <CentralAZ:ScheduledLocationItemPicker ID="slpLocation" runat="server" Label="Location" Required="false" Enabled="false" AllowMultiSelect="false" OnSelectItem="slpLocation_SelectItem" ValidationGroup="ReservationLocation" />
                        <div class="col-md-12 xs-text-center" style="width: 200px;">
                            <div class="photo">
                                <asp:Literal ID="lImage" runat="server" />
                            </div>
                        </div>
                    </div>
                    <div class="col-md-6">
                        <Rock:NotificationBox ID="nbLocationConflicts" Visible="false" NotificationBoxType="Danger" runat="server" />

                        <div class="grid">
                            <Rock:Grid ID="gLocationLayouts" runat="server" AllowPaging="false" DisplayType="Light" RowItemText="Location Layout" OnRowDataBound="gLocationLayouts_RowDataBound">
                                <Columns>
                                    <Rock:RockTemplateField>
                                        <ItemTemplate>
                                            <asp:HiddenField ID="hfLayoutId" runat="server" />
                                            <asp:RadioButton ID="rbSelected" runat="server" OnCheckedChanged="rbSelected_CheckedChanged" AutoPostBack="true" />
                                        </ItemTemplate>
                                    </Rock:RockTemplateField>
                                    <Rock:RockBoundField DataField="Name" HeaderText="Layout" />
                                    <Rock:RockBoundField DataField="Description" HeaderText="Description" />
                                    <Rock:RockTemplateField HeaderText="Photo">
                                        <ItemTemplate>
                                            <asp:Literal ID="lPhoto" runat="server" />
                                        </ItemTemplate>
                                    </Rock:RockTemplateField>
                                </Columns>
                            </Rock:Grid>
                        </div>
                    </div>

                </div>
            </Content>
        </Rock:ModalDialog>

        <Rock:ModalDialog ID="dlgReservationResource" runat="server" Title="Select Resource" OnSaveThenAddClick="dlgReservationResource_SaveThenAddClick" OnSaveClick="dlgReservationResource_SaveClick" OnCancelScript="clearActiveDialog();" ValidationGroup="ReservationResource">
            <Content>
                <asp:HiddenField ID="hfAddReservationResourceGuid" runat="server" />
                <asp:ValidationSummary ID="valReservationResourceSummary" runat="server" HeaderText="Please Correct the Following" CssClass="alert alert-danger" ValidationGroup="ReservationResource" />
                <Rock:NotificationBox ID="nbResourceNote" Visible="false" NotificationBoxType="Info" runat="server" />
                <div class="row">
                    <div class="col-md-6">
                        <CentralAZ:ScheduledResourcePicker ID="srpResource" runat="server" Label="Resource" Required="false" Enabled="false" AllowMultiSelect="false" OnSelectItem="srpResource_SelectItem" ValidationGroup="ReservationResource" />
                    </div>
                    <div class="col-md-6">
                        <Rock:NumberBox ID="nbQuantity" runat="server" NumberType="Integer" MinimumValue="1" ValidationGroup="ReservationResource" Label="Quantity" />
                        <Rock:NotificationBox ID="nbResourceConflicts" Visible="false" NotificationBoxType="Warning" runat="server" />
                    </div>
                </div>
            </Content>
        </Rock:ModalDialog>
    </ContentTemplate>
</asp:UpdatePanel>
