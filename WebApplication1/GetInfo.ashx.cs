using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Web;
using Dapper;
using Newtonsoft.Json;

namespace WebApplication1
{
    public class GetInfo : IHttpHandler
    {
        public List<Node> Operate = new List<Node>
        {
            new Node(1, "编辑表结构"),
            new Node(2, "查看前一百条数据"),
            new Node(3, "设置条件查看数据"),
            new Node(4, "查看后100条数据(只针对有自增键的表)")
        };

        private object fieldType = new
        {

        };

        public void ProcessRequest(HttpContext context)
        {
            if (context.Request["ajaxName"] == "GetDbList")
            {
                GetDbList(context);
            }
            else if (context.Request["ajaxName"] == "GetTableList")
            {
                GetTableList(context);
            }
            else if (context.Request["ajaxName"] == "GetTableInfo")
            {
                GetTableInfo(context);
            }else if(context.Request["ajaxName"]=="SubmitRow"){
                SubmitRow(context);
            }
        }

        public void SubmitRow(HttpContext context)
        {
            var nodeList = JsonConvert.DeserializeObject<TableInfo>(context.Request["rowData"]);

            context.Response.Write(JsonConvert.SerializeObject(nodeList));
            context.Response.End();
        }

        public void GetDbList(HttpContext context)
        {
            var dbList = DbClient.Query<string>(@"SELECT name
                                                           FROM   master.dbo.sysdatabases
                                                           WHERE  name NOT IN ( 'master', 'tempdb', 'model', 'msdb' )
                                                           ORDER BY name;").ToList();

            var nodeList = dbList.Select(x => new Node
            {
                id = dbList.IndexOf(x),
                label = x,
                children = new List<Node> { new Node
                {
                    children = new List<Node>(),
                    id = Convert.ToInt32(dbList.IndexOf(x).ToString()+"1"),
                    label = "加载中..."
                } }
            });
            context.Response.Write(JsonConvert.SerializeObject(nodeList));
            context.Response.End();
        }

        public void GetTableList(HttpContext context)
        {
            var id = context.Request["id"];
            var label = context.Request["label"];
            var tableList = DbClient.Query<string>($@"USE {label}
                                                      SELECT d.name
                                                      FROM   syscolumns a
                                                             LEFT JOIN systypes b ON a.xtype = b.xusertype
                                                             INNER JOIN sysobjects d ON a.id = d.id
                                                                                        AND d.xtype = 'U'
                                                                                        AND d.name <> 'dtproperties'
                                                             LEFT JOIN syscomments e ON a.cdefault = e.id
                                                             LEFT JOIN sys.extended_properties g ON a.id = g.major_id
                                                                                                    AND a.colid = g.minor_id
                                                      GROUP BY d.name
	                                                  ORDER BY d.name;").ToList();
            var nodeList = tableList.Select(x => new Node
            {
                id = Convert.ToInt32(id + tableList.IndexOf(x)),
                label = x,
                children = Operate
            });
            context.Response.Write(JsonConvert.SerializeObject(nodeList));
            context.Response.End();
        }

        public void GetTableInfo(HttpContext context)
        {
            var parentLabel = context.Request["parentLabel"];
            var label = context.Request["label"];
            var tableInfo = DbClient.Query<TableInfo>($@"USE {parentLabel};
                                                     SELECT a.name AS fieldname ,
                                                            ( CASE WHEN COLUMNPROPERTY(a.id, a.name, 'IsIdentity') = 1
                                                                   THEN '√'
                                                                   ELSE ''
                                                              END ) AS identifying ,
                                                            ( CASE WHEN ( SELECT    COUNT(*)
                                                                          FROM      sysobjects--查询主键  
                                                                          WHERE     ( name IN (
                                                                                      SELECT    name
                                                                                      FROM      sysindexes
                                                                                      WHERE     ( id = a.id )
                                                                                                AND ( indid IN (
                                                                                                      SELECT  indid
                                                                                                      FROM    sysindexkeys
                                                                                                      WHERE   ( id = a.id )
                                                                                                              AND ( colid IN (
                                                                                                              SELECT
                                                                                                              colid
                                                                                                              FROM
                                                                                                              syscolumns
                                                                                                              WHERE
                                                                                                              ( id = a.id )
                                                                                                              AND ( name = a.name ) ) ) ) ) ) )
                                                                                    AND ( xtype = 'PK' )
                                                                        ) > 0 THEN '√'
                                                                   ELSE ''
                                                              END ) AS primarykey ,
                                                            b.name AS [types] ,
                                                            COLUMNPROPERTY(a.id,a.name,'PRECISION') as  lengths,
                                                            ( CASE WHEN a.isnullable = 1 THEN '√'
                                                                   ELSE ''
                                                              END ) AS ornull ,
                                                            ISNULL(e.text, '') AS [defaults] ,
                                                            ISNULL(g.[value], '') AS describe
                                                     FROM   syscolumns a
                                                            LEFT JOIN systypes b ON a.xtype = b.xusertype
                                                            INNER JOIN sysobjects d ON a.id = d.id
                                                                                       AND d.xtype = 'U'
                                                                                       AND d.name <> 'dtproperties'
                                                            LEFT JOIN syscomments e ON a.cdefault = e.id
                                                            LEFT JOIN sys.extended_properties g ON a.id = g.major_id
                                                                                                   AND a.colid = g.minor_id
                                                     WHERE  d.name = '{label}'
                                                     ORDER BY a.id ,
                                                            a.colorder;
                                                ").ToList();
            context.Response.Write(JsonConvert.SerializeObject(tableInfo.Select(x => new TableInfo
            {
                fieldname = x.fieldname,
                identifying = x.identifying,
                describe = x.describe,
                defaults = x.defaults.Trim('(').Trim(')').Trim('\''),
                primarykey = x.primarykey,
                ornull = x.ornull,
                types = x.types.ToLower().Contains("char") ? $"{x.types}({x.lengths})" : x.types,
            })));
            context.Response.End();
        }


        public bool IsReusable => false;

    }

    public class Node
    {
        public Node()
        {

        }

        public Node(int id, string label)
        {
            this.id = id;
            this.label = label;
        }

        /// <summary>
        /// 各个节点之前的区分
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public int id { get; set; }
        /// <summary>
        /// 节点名称
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public string label { get; set; }
        /// <summary>
        /// 子节点
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public List<Node> children { get; set; }
    }

    public class TableInfo
    {
        public string newfield { get; set; }
        // ReSharper disable once InconsistentNaming
        public string fieldname { get; set; }
        // ReSharper disable once InconsistentNaming
        public string identifying { get; set; }
        // ReSharper disable once InconsistentNaming
        public string primarykey { get; set; }
        // ReSharper disable once InconsistentNaming
        public string types { get; set; }
        // ReSharper disable once InconsistentNaming
        public string lengths { get; set; }
        // ReSharper disable once InconsistentNaming
        public string ornull { get; set; }
        // ReSharper disable once InconsistentNaming
        public string defaults { get; set; }
        // ReSharper disable once InconsistentNaming
        public string describe { get; set; }
    }




    public class DbClient
    {

        public static IEnumerable<T> Query<T>(string sql, object param = null)
        {
            if (string.IsNullOrEmpty(sql))
            {
                throw new ArgumentNullException(nameof(sql));
            }
            using (IDbConnection con = DataSource.GetConnection())
            {
                IEnumerable<T> tList = con.Query<T>(sql, param);
                con.Close();
                return tList;
            }
        }

        public static int Excute(string sql, object param = null, IDbTransaction transaction = null)
        {
            if (string.IsNullOrEmpty(sql))
            {
                throw new ArgumentNullException(nameof(sql));
            }
            using (IDbConnection con = DataSource.GetConnection())
            {
                return con.Execute(sql, param, transaction);
            }
        }
        public static T ExecuteScalar<T>(string sql, object param = null)
        {
            if (string.IsNullOrEmpty(sql))
            {
                throw new ArgumentNullException(nameof(sql));
            }
            using (IDbConnection con = DataSource.GetConnection())
            {
                return con.ExecuteScalar<T>(sql, param);
            }
        }

        public static T ExecuteScalarProc<T>(string strProcName, object param = null)
        {
            using (IDbConnection con = DataSource.GetConnection())
            {
                return (T)con.ExecuteScalar(strProcName, param, commandType: CommandType.StoredProcedure);
            }
        }

        /// <summary>
        /// 执行带参数的存储过程(查询)
        /// </summary>
        /// <param name="strProcName"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public static IEnumerable<T> ExecuteQueryProc<T>(string strProcName, object param = null)
        {
            using (IDbConnection con = DataSource.GetConnection())
            {
                IEnumerable<T> tList = con.Query<T>(strProcName, param, commandType: CommandType.StoredProcedure);
                con.Close();
                return tList;
            }
        }

        /// <summary>
        /// 执行带参数的存储过程
        /// </summary>
        /// <param name="strProcName"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public static int ExecuteProc(string strProcName, object param = null)
        {
            try
            {
                using (IDbConnection con = DataSource.GetConnection())
                {
                    return con.Execute(strProcName, param, commandType: CommandType.StoredProcedure);
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }

    public class DataSource
    {
        public static string ConnString = ConfigurationManager.ConnectionStrings["DBS"].ConnectionString;
        public static IDbConnection GetConnection()
        {
            if (string.IsNullOrEmpty(ConnString))
                throw new NoNullAllowedException(nameof(ConnString));
            return new SqlConnection(ConnString);
        }
    }
}