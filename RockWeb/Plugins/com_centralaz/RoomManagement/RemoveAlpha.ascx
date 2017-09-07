<%@ Control Language="C#" AutoEventWireup="true" CodeFile="RemoveAlpha.ascx.cs" Inherits="RockWeb.Plugins.com_centralaz.RoomManagement.RemoveAlpha" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <asp:Panel ID="pnlView" runat="server" CssClass="panel panel-block">

            <div class="panel-heading">
                <h1 class="panel-title"><i class="fa fa-star"></i>Blank Detail Block</h1>

                <div class="panel-labels">
                    <Rock:HighlightLabel ID="hlblTest" runat="server" LabelType="Info" Text="Label" />
                </div>
            </div>
            <Rock:PanelDrawer ID="pdAuditDetails" runat="server"></Rock:PanelDrawer>
            <div class="panel-body">

                <div class="alert alert-info">
                    <h4>Remove Room Alpha Code</h4>

                </div>
                <Rock:BootstrapButton ID="lUninstall" runat="server" CssClass="btn btn-primary" OnClick="lUninstall_Click" Text="Uninstall Alpha" />
            </div>

        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>
