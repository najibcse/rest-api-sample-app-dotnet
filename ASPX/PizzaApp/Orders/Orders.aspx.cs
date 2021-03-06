﻿using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Data;
using System.Data.SQLite;
using PayPal;
using PayPal.Api.Payments;

namespace PizzaApp
{
    public partial class Orders : System.Web.UI.Page
    {
        #region Data
        DataAccessLayer dataAccessObject;

        private DataTable GetUser(string email)
        {
            DataTable datTable = new DataTable();
            StringBuilder sqliteQuerySelect = new StringBuilder();
            sqliteQuerySelect.Append("SELECT ");
            sqliteQuerySelect.Append("id, ");
            sqliteQuerySelect.Append("email, ");
            sqliteQuerySelect.Append("encrypted_password, ");
            sqliteQuerySelect.Append("sign_in_count, ");
            sqliteQuerySelect.Append("credit_card_id ");
            sqliteQuerySelect.Append("FROM users ");
            sqliteQuerySelect.Append("WHERE email = @email");
            SQLiteDataAdapter sqliteDataAdapterSelect = new SQLiteDataAdapter();
            sqliteDataAdapterSelect.SelectCommand = new SQLiteCommand();
            sqliteDataAdapterSelect.SelectCommand.Parameters.AddWithValue("@email", email);
            dataAccessObject = new DataAccessLayer();
            datTable = dataAccessObject.Select(sqliteQuerySelect.ToString(), sqliteDataAdapterSelect);
            return datTable;
        }

        private DataTable GetOrders(int userID)
        {
            DataTable datTable = new DataTable();
            StringBuilder sqliteQuerySelect = new StringBuilder();
            sqliteQuerySelect.Append("SELECT ");
            sqliteQuerySelect.Append("id, ");
            sqliteQuerySelect.Append("user_id, ");
            sqliteQuerySelect.Append("payment_id, ");
            sqliteQuerySelect.Append("state, ");
            sqliteQuerySelect.Append("amount, ");
            sqliteQuerySelect.Append("description, ");
            sqliteQuerySelect.Append("created_at, ");
            sqliteQuerySelect.Append("updated_at ");
            sqliteQuerySelect.Append("FROM orders ");
            sqliteQuerySelect.Append("WHERE user_id = @user_id ");
            sqliteQuerySelect.Append("ORDER BY updated_at DESC, id DESC");
            SQLiteDataAdapter sqliteDataAdapterSelect = new SQLiteDataAdapter();
            sqliteDataAdapterSelect.SelectCommand = new SQLiteCommand();
            sqliteDataAdapterSelect.SelectCommand.Parameters.AddWithValue("@user_id", userID);
            dataAccessObject = new DataAccessLayer();
            datTable = dataAccessObject.Select(sqliteQuerySelect.ToString(), sqliteDataAdapterSelect);
            return datTable;
        }

        private DataTable GetPaymentID(string orderID)
        {
            DataTable datTable = new DataTable();
            string sqliteQuery = "SELECT payment_id FROM orders WHERE id = @id";
            SQLiteDataAdapter sqliteDataAdapterSelect = new SQLiteDataAdapter();
            sqliteDataAdapterSelect.SelectCommand = new SQLiteCommand();
            sqliteDataAdapterSelect.SelectCommand.Parameters.AddWithValue("@id", orderID);
            dataAccessObject = new DataAccessLayer();
            datTable = dataAccessObject.Select(sqliteQuery, sqliteDataAdapterSelect);
            return datTable;
        }

        private int GetSignedInUserID(string email)
        {
            int userID = 0;
            DataTable datTable = GetUser(email);
            if (datTable != null && datTable.Rows.Count > 0)
            {
                var distinctRows = from DataRow dRow in datTable.Rows
                                   where dRow.Field<string>("email") == email
                                   select new { column1 = dRow["id"] };
                if (distinctRows != null)
                {
                    foreach (var row in distinctRows)
                    {
                        userID = Convert.ToInt32(row.column1);
                        break;
                    }
                }
            }
            return userID;
        }

        private string GetOrdersPaymentID(string orderID)
        {
            string paymentID = string.Empty;
            DataTable datTable = GetPaymentID(orderID);
            if (datTable != null && datTable.Rows.Count > 0)
            {
                var distinctRows = (from DataRow dRow in datTable.Rows
                                    select new { column1 = dRow["payment_id"] }).Distinct();
                if (distinctRows != null)
                {
                    foreach (var row in distinctRows)
                    {
                        paymentID = Convert.ToString(row.column1);
                        break;
                    }
                }
            }
            return paymentID;
        }

        private bool Update(int orderID, string state, string updatedAt)
        {
            bool isSuccess = false;
            int rowsAffacted = 0;
            StringBuilder sqliteQueryUpdate = new StringBuilder();
            sqliteQueryUpdate.Append("UPDATE orders ");
            sqliteQueryUpdate.Append("SET ");
            sqliteQueryUpdate.Append("state = @state, ");
            sqliteQueryUpdate.Append("updated_at = @updated_at ");
            sqliteQueryUpdate.Append("WHERE ");
            sqliteQueryUpdate.Append("id = @id");
            SQLiteDataAdapter sqliteDataAdapterUpdate = new SQLiteDataAdapter();
            sqliteDataAdapterUpdate.UpdateCommand = new SQLiteCommand();
            sqliteDataAdapterUpdate.UpdateCommand.Parameters.AddWithValue("@state", state);
            sqliteDataAdapterUpdate.UpdateCommand.Parameters.AddWithValue("@updated_at", updatedAt);
            sqliteDataAdapterUpdate.UpdateCommand.Parameters.AddWithValue("@id", orderID);
            dataAccessObject = new DataAccessLayer();
            rowsAffacted = dataAccessObject.Update(sqliteQueryUpdate.ToString(), sqliteDataAdapterUpdate);
            if (rowsAffacted > 0)
            {
                isSuccess = true;
            }
            return isSuccess;
        }
        #endregion

        #region PayPal
        private string AccessToken
        {
            get
            {
                string token = new OAuthTokenCredential
                                (
                                   "EBWKjlELKMYqRNQ6sYvFo64FtaRLRR5BdHEESmha49TM",
                                    "EO422dn3gQLgDbuwqTjzrFgFtaRLRR5BdHEESmha49TM",
                                    Configuration.GetConfiguration()
                                ).GetAccessToken();
                return token;
            }
        }

        private APIContext Api
        {
            get
            {
                APIContext context = new APIContext(AccessToken);
                context.Config = Configuration.GetConfiguration();
                return context;
            }
        }
        #endregion
        
        #region Event Handlers
        protected void Page_Init(Object sender, EventArgs e)
        {
            if (HttpContext.Current.User.Identity.IsAuthenticated)
            {
                if (Request.QueryString["OrderId"] != null && Request.QueryString["Success"] != null)
                {
                    var orderID = Request.QueryString["OrderId"];
                    var payerID = Request.QueryString["PayerId"];
                    var isSuccess = Convert.ToBoolean(Request.QueryString["Success"]);
                    if (isSuccess)
                    {
                        PaymentExecution pymntExecution = new PaymentExecution();
                        pymntExecution.payer_id = payerID;
                        Payment pymnt = new Payment();
                        pymnt.id = GetOrdersPaymentID(orderID);
                        Payment pay = null;
                        try
                        {
                            pay = pymnt.Execute(Api, pymntExecution);
                            if (pay != null && pay.state.Trim().ToLower().Equals("approved"))
                            {
                                var state = pay.state.Trim();
                                var updatedAtDateTime = Convert.ToDateTime(pay.create_time);
                                var updatedAt = updatedAtDateTime.ToString("yyyy-MM-dd hh:mm:ss.FFFFF");
                                var ordID = Convert.ToInt32(orderID);
                                bool isUpdated = Update(ordID, state, updatedAt);
                            }
                        }
                        catch (Exception ex)
                        {
                            divAlertMessage.Visible = true;
                            divAlertMessage.Attributes["class"] = "alert fade in alert-error";
                            LabelAlertMessage.Text = ex.Message;
                        }
                    }
                    else
                    {
                        orderID = Request.QueryString["OrderId"];
                        var updatedAtDateTime = DateTime.Now;
                        var updatedAt = updatedAtDateTime.ToString("yyyy-MM-dd hh:mm:ss.FFFFF");
                        bool isUpdated = Update(Convert.ToInt32(orderID), "cancelled", updatedAt);
                    }
                }
            }
            else
            {
                Response.Redirect("~/Account/SignIn.aspx");
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (HttpContext.Current.User.Identity.IsAuthenticated)
            {
                var email = HttpContext.Current.User.Identity.Name.Trim();

                if (!IsPostBack)
                {
                    int userID = GetSignedInUserID(email);
                    DataTable datTable = GetOrders(userID);
                    if (datTable != null && datTable.Rows.Count > 0)
                    {
                        GridViewOrders.DataSource = datTable;
                        GridViewOrders.DataBind();
                    }
                    else
                    {
                        divAlertMessage.Visible = true;
                        divAlertMessage.Attributes["class"] = "alert fade in alert-error";
                        LabelAlertMessage.Text = "There are no order records.";
                    }
                }
            }
        }
        #endregion
    }
}