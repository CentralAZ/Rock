<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ReservationLava.ascx.cs" Inherits="RockWeb.Plugins.com_centralaz.RoomManagement.ReservationLava" %>
<%@ Register TagPrefix="CentralAZ" Assembly="com.centralaz.RoomManagement" Namespace="com.centralaz.RoomManagement.Web.UI.Controls" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <Rock:NotificationBox ID="nbMessage" runat="server" Visible="false" />

        <asp:Panel ID="pnlDetails" runat="server" CssClass="row">

            <asp:Panel ID="pnlFilters" CssClass="col-md-3 hidden-print" runat="server">

                <asp:Panel ID="pnlCalendar" CssClass="calendar" runat="server">
                    <asp:Calendar ID="calReservationCalendar" runat="server" DayNameFormat="FirstLetter" SelectionMode="Day" BorderStyle="None"
                        TitleStyle-BackColor="#ffffff" NextPrevStyle-ForeColor="#333333" FirstDayOfWeek="Sunday" Width="100%" CssClass="calendar-month" OnSelectionChanged="calReservationCalendar_SelectionChanged" OnDayRender="calReservationCalendar_DayRender" OnVisibleMonthChanged="calReservationCalendar_VisibleMonthChanged">
                        <DayStyle CssClass="calendar-day" />
                        <TodayDayStyle CssClass="calendar-today" />
                        <SelectedDayStyle CssClass="calendar-selected" BackColor="Transparent" />
                        <OtherMonthDayStyle CssClass="calendar-last-month" />
                        <DayHeaderStyle CssClass="calendar-day-header" />
                        <NextPrevStyle CssClass="calendar-next-prev" />
                        <TitleStyle CssClass="calendar-title" />
                    </asp:Calendar>
                </asp:Panel>

                <% if ( LocationPanelOpen || LocationPanelClosed )
                    { %>
                <div class="panel panel-default">
                    <div class="panel-heading">
                        <a role="button" data-toggle="collapse" href="#collapseOne">
                            <h4 class="panel-title">Locations                                
                            </h4>
                        </a>
                    </div>
                    <div id="collapseOne" class='<%= LocationPanelOpen ? "panel-collapse collapse in" : "panel-collapse collapse out" %>'>
                        <div class="panel-body">
                            <% } %>

                            <Rock:LocationItemPicker ID="lipLocation" runat="server" Label="Filter by Locations" AllowMultiSelect="true" OnSelectItem="lipLocation_SelectItem" />

                            <% if ( LocationPanelOpen || LocationPanelClosed )
                                { %>
                        </div>
                    </div>
                </div>
                <% } %>

                <% if ( ResourcePanelOpen || ResourcePanelClosed )
                    { %>
                <div class="panel panel-default">
                    <div class="panel-heading">
                        <a role="button" data-toggle="collapse" href="#collapseTwo">
                            <h4 class="panel-title">Resources                                
                            </h4>
                        </a>
                    </div>
                    <div id="collapseTwo" class='<%= ResourcePanelOpen ? "panel-collapse collapse in" : "panel-collapse collapse out" %>'>
                        <div class="panel-body">
                            <% } %>

                            <CentralAZ:ResourcePicker ID="rpResource" runat="server" Label="Filter by Resources" AllowMultiSelect="true" OnSelectItem="rpResource_SelectItem" />

                            <% if ( ResourcePanelOpen || ResourcePanelClosed )
                                { %>
                        </div>
                    </div>
                </div>
                <% } %>

                <% if ( CampusPanelOpen || CampusPanelClosed )
                    { %>
                <div class="panel panel-default">
                    <div class="panel-heading">
                        <a role="button" data-toggle="collapse" href="#collapseThree">
                            <h4 class="panel-title">Campuses                                
                            </h4>
                        </a>
                    </div>
                    <div id="collapseThree" class='<%= CampusPanelOpen ? "panel-collapse collapse in" : "panel-collapse collapse out" %>'>
                        <div class="panel-body">
                            <% } %>

                            <%-- Note: RockControlWrapper/Div/CheckboxList is being used instead of just a RockCheckBoxList, because autopostback does not currently work for RockControlCheckbox--%>
                            <Rock:RockControlWrapper ID="rcwCampus" runat="server" Label="Filter by Campus">
                                <div class="controls">
                                    <asp:CheckBoxList ID="cblCampus" RepeatDirection="Vertical" runat="server" DataTextField="Name" DataValueField="Id"
                                        OnSelectedIndexChanged="cblCampus_SelectedIndexChanged" AutoPostBack="true" />
                                </div>
                            </Rock:RockControlWrapper>

                            <% if ( CampusPanelOpen || CampusPanelClosed )
                                { %>
                        </div>
                    </div>
                </div>
                <% } %>

                <% if ( MinistryPanelOpen || MinistryPanelClosed )
                    { %>
                <div class="panel panel-default">
                    <div class="panel-heading">
                        <a role="button" data-toggle="collapse" href="#collapseFour">
                            <h4 class="panel-title">Ministries                            
                            </h4>
                        </a>
                    </div>
                    <div id="collapseFour" class='<%= MinistryPanelOpen ? "panel-collapse collapse in" : "panel-collapse collapse out" %>'>
                        <div class="panel-body">
                            <% } %>

                            <Rock:RockControlWrapper ID="rcwMinistry" runat="server" Label="Filter by Ministry">
                                <div class="controls">
                                    <asp:CheckBoxList ID="cblMinistry" RepeatDirection="Vertical" runat="server" DataTextField="Name" DataValueField="Id" OnSelectedIndexChanged="cblMinistry_SelectedIndexChanged" AutoPostBack="true" />
                                </div>
                            </Rock:RockControlWrapper>

                            <% if ( MinistryPanelOpen || MinistryPanelClosed )
                                { %>
                        </div>
                    </div>
                </div>
                <% } %>

                <% if ( ApprovalPanelOpen || ApprovalPanelClosed )
                    { %>
                <div class="panel panel-default">
                    <div class="panel-heading">
                        <a role="button" data-toggle="collapse" href="#collapseFive">
                            <h4 class="panel-title">Statuses                            
                            </h4>
                        </a>
                    </div>
                    <div id="collapseFive" class='<%= ApprovalPanelOpen ? "panel-collapse collapse in" : "panel-collapse collapse out" %>'>
                        <div class="panel-body">
                            <% } %>

                            <Rock:RockControlWrapper ID="rcwApproval" runat="server" Label="Filter by Approval State">
                                <div class="controls">
                                    <asp:CheckBoxList ID="cblApproval" RepeatDirection="Vertical" runat="server" OnSelectedIndexChanged="cblApproval_SelectedIndexChanged" AutoPostBack="true" />
                                </div>
                            </Rock:RockControlWrapper>

                            <% if ( ApprovalPanelOpen || ApprovalPanelClosed )
                                { %>
                        </div>
                    </div>
                </div>
                <% } %>

                <% if ( ReservationTypePanelOpen || ReservationTypePanelClosed )
                    { %>
                <div class="panel panel-default">
                    <div class="panel-heading">
                        <a role="button" data-toggle="collapse" href="#collapseSix">
                            <h4 class="panel-title">Reservation Types                                
                            </h4>
                        </a>
                    </div>
                    <div id="collapseSix" class='<%= ReservationTypePanelOpen ? "panel-collapse collapse in" : "panel-collapse collapse out" %>'>
                        <div class="panel-body">
                            <% } %>

                            <%-- Note: RockControlWrapper/Div/CheckboxList is being used instead of just a RockCheckBoxList, because autopostback does not currently work for RockControlCheckbox--%>
                            <Rock:RockControlWrapper ID="rcwReservationType" runat="server" Label="Filter by Reservation Type">
                                <div class="controls">
                                    <asp:CheckBoxList ID="cblReservationType" RepeatDirection="Vertical" runat="server" DataTextField="Name" DataValueField="Id"
                                        OnSelectedIndexChanged="cblReservationType_SelectedIndexChanged" AutoPostBack="true" />
                                </div>
                            </Rock:RockControlWrapper>

                            <% if ( ReservationTypePanelOpen || ReservationTypePanelClosed )
                                { %>
                        </div>
                    </div>
                </div>
                <% } %>

                <Rock:DatePicker ID="dpStartDate" runat="server" Label="Start Date" OnTextChanged="dpStartDate_TextChanged" AutoPostBack="true" />
                <Rock:DatePicker ID="dpEndDate" runat="server" Label="End Date" OnTextChanged="dpEndDate_TextChanged" AutoPostBack="true" />

                <small class="text-muted">v<asp:Literal ID="lVersionText" runat="server"></asp:Literal></small>
            </asp:Panel>

            <asp:Panel ID="pnlList" CssClass="col-md-9" runat="server">

                <div class="btn-group hidden-print" role="group">
                    <Rock:BootstrapButton ID="btnDay" runat="server" CssClass="btn btn-default" Text="Day" OnClick="btnViewMode_Click" />
                    <Rock:BootstrapButton ID="btnWeek" runat="server" CssClass="btn btn-default" Text="Week" OnClick="btnViewMode_Click" />
                    <Rock:BootstrapButton ID="btnMonth" runat="server" CssClass="btn btn-default" Text="Month" OnClick="btnViewMode_Click" />
                </div>

                <div class="pull-right">
                    <asp:LinkButton ID="lbPrint" runat="server" CssClass="btn btn-link" Text="Print" OnClick="lbPrint_Click" />
                </div>

                <asp:Literal ID="lOutput" runat="server"></asp:Literal>
                <asp:Literal ID="lDebug" Visible="false" runat="server"></asp:Literal>

            </asp:Panel>

        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>
