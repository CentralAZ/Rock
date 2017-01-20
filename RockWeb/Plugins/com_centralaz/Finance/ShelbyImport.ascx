<%@ Control Language="C#" AutoEventWireup="true" CodeFile="ShelbyImport.ascx.cs" Inherits="RockWeb.Plugins.com_centralaz.Finance.ShelbyImport" %>

<script src="/SignalR/hubs"></script>
<script type="text/javascript">
    $(function () {
        var proxy = $.connection.rockMessageHub;

        proxy.client.showLog = function () {
            $("div[id$='_messageContainer']").fadeIn();
            $("div[id$='_pnlConfiguration']").fadeOut();
        }

        proxy.client.receiveNotification = function (name, message) {
            if (name.startsWith("shelbyImport-"))
            {
                var fields = name.split("-");
                $("#"+fields[1]).html(message);
            }
        }

        $.connection.hub.start().done(function () {
            // hub started... do stuff here if you want to let the user know something
            console.log("SignalR hub started.");
        });
    })
</script>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>

        <asp:Panel ID="pnlImport" runat="server" CssClass="panel panel-block">

            <div class="panel-heading">
                <h1 class="panel-title">Import Shelby Contributions</h1>
            </div>
            <div class="panel-body">

                <Rock:NotificationBox ID="nbMessage" runat="server" NotificationBoxType="Danger" />

                <asp:Panel ID="pnlConfiguration" runat="server">
                    <h2>Settings</h2>
                    <Rock:RockTextBox runat="server" ID="tbBatchName" Label="Batch Name" ToolTip="The name you wish to use for this batch import." OnTextChanged="tbBatchName_TextChanged" AutoPostBack="true"></Rock:RockTextBox>

                    <Rock:CampusPicker ID="cpCampus" runat="server" Label="Campus" Required="true" ToolTip="The campus you are assigning to the batch." />

                    <h2>Verify/Set Account Mapping</h2>
                    <table class="table table-striped table-hover table-condensed">
                    <asp:Repeater ID="rptAccountMap" runat="server" OnItemDataBound="rptAccountMap_ItemDataBound">
                        <HeaderTemplate></HeaderTemplate>
                        <ItemTemplate>
                            <tr>
                                <td class="col-md-4">
                                    <asp:Literal ID="litFundName" runat="server"></asp:Literal>
                                    <asp:HiddenField ID="hfFundId" runat="server" />
                                    <span class="pull-right"><asp:Literal ID="litAccontSaveStatus" runat="server"></asp:Literal></span>
                                </td>
                                <td class="col-md-8">
                                    <Rock:RockDropDownList ID="rdpAcccounts" runat="server" AutoPostBack="true" OnSelectedIndexChanged="rdpAcccounts_SelectedIndexChanged"></Rock:RockDropDownList>
                                </td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                    </table>

                </asp:Panel>

                <p>
                    <asp:LinkButton runat="server" ID="lbImport" CssClass="btn btn-primary" OnClick="lbImport_Click">
                        <i class="fa fa-arrow-up"></i> Import
                    </asp:LinkButton>
                </p>

                <!-- SignalR client notification area -->
                <div class="well" id="messageContainer" runat="server" style="display:none;">
                    <div id="processingUsers"></div>
                    <div id="processingBatches"></div>
                    <div id="processingTransactions"></div>
                </div>

                <asp:Panel ID="pnlErrors" runat="server" Visible="false" CssClass="alert alert-danger block-message error">
                    <Rock:Grid ID="gErrors" runat="server" AllowSorting="false" OnRowDataBound="gErrors_RowDataBound" RowItemText="error" AllowPaging="false" RowStyle-CssClass="danger" AlternatingRowStyle-CssClass="danger" ShowActionRow="false">
                        <Columns>
                            <asp:TemplateField SortExpression="ReferenceNumber" HeaderText="Reference Number">
                                <ItemTemplate>
                                    <asp:Literal ID="lReferenceNumber" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField SortExpression="ChurchCode" HeaderText="Church Code" Visible="false">
                                <ItemTemplate>
                                    <asp:Literal ID="lChurchCode" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField SortExpression="IndividualId" HeaderText="Individual ID">
                                <ItemTemplate>
                                    <asp:Literal ID="lIndividualId" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField SortExpression="ContributorName" HeaderText="Contributor Name">
                                <ItemTemplate>
                                    <asp:Literal ID="lContributorName" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField SortExpression="FundName" HeaderText="Fund Name">
                                <ItemTemplate>
                                    <asp:Literal ID="lFundName" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField SortExpression="FundCode" HeaderText="Fund Code">
                                <ItemTemplate>
                                    <asp:Literal ID="lFundCode" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField SortExpression="ReceivedDate" HeaderText="Received Date">
                                <ItemTemplate>
                                    <asp:Literal ID="lReceivedDate" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField SortExpression="Amount" HeaderText="Amount">
                                <ItemTemplate>
                                    <asp:Literal ID="lAmount" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField SortExpression="TransactionId" HeaderText="Transaction ID">
                                <ItemTemplate>
                                    <asp:Literal ID="lTransactionId" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField SortExpression="ContributionType" HeaderText="Contribution Type">
                                <ItemTemplate>
                                    <asp:Literal ID="lContributionType" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField SortExpression="Error" HeaderText="Error">
                                <ItemTemplate>
                                    <asp:Literal ID="lError" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </Rock:Grid>
                </asp:Panel>

                <Rock:NotificationBox ID="nbBatch" runat="server" NotificationBoxType="Success" />

            </div>
        </asp:Panel>

        <asp:Panel ID="pnlGrid" runat="server" CssClass="panel panel-block" Visible="false">
            <div class="panel-heading">
                <h1 class="panel-title">Batch Summary</h1>
            </div>
            <div class="panel-body">
                <div class="grid grid-panel">
                    <Rock:Grid ID="gContributions" runat="server" AllowSorting="false" AllowPaging="false">
                        <Columns>
                            <asp:TemplateField SortExpression="TransactionId" HeaderText="Transaction ID">
                                <ItemTemplate>
                                    <asp:Literal ID="lTransactionID" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="Transaction.ProcessedDateTime" HeaderText="Transaction Date" SortExpression="TransactionDate" />
                            <asp:TemplateField SortExpression="FullName" HeaderText="Full Name">
                                <ItemTemplate>
                                    <asp:Literal ID="lFullName" runat="server" />
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="Transaction.FinancialPaymentDetail.CurrencyTypeValue" HeaderText="Transaction Type" SortExpression="TransactionType" />
                            <asp:BoundField DataField="Account" HeaderText="Fund Name" SortExpression="FundName" />
                            <asp:BoundField DataField="Amount" HeaderText="Amount" SortExpression="Amount" />
                        </Columns>
                    </Rock:Grid>
                </div>
            </div>

        </asp:Panel>

    </ContentTemplate>
</asp:UpdatePanel>
