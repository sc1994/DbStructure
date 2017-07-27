using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using Dapper;
using Newtonsoft.Json;

namespace WebApplication1
{
    public class GetInfo : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            var name = context.Request["ajaxName"];
            if (name == "GetDbList")
            {
                GetDbList(context);
            }
            else if (name == "GetTableList")
            {
                GetTableList(context);
            }
            else if (name == "GetTableInfo")
            {
                GetTableInfo(context);
            }
            else if (name == "SubmitRow")
            {
                SubmitRow(context);
            }
            else if (name == "EditTableDescribe")
            {
                EditTableDescribe(context);
            }
        }

        public void EditTableDescribe(HttpContext context)
        {
            var name = context.Request["tableName"];
            var describe = context.Request["tableDescribe"];
            // todo 因为无法判断当前是否已有描述 出此下策 目前是先执行更新出现异常再尝试添加
            // 执行 更新
            try
            {
                DbClient.Excute($@"USE ThreeArriveAction
                                  EXEC sp_updateextendedproperty @name = N'MS_Description',
                                                                 @value = N'{describe}',
                                                                 @level0type = N'user',
                                                                 @level0name = N'dbo',
                                                                 @level1type = N'table',
                                                                 @level1name = N'{name}';");
            }
            catch (Exception)
            {
                // 执行 添加
                DbClient.Excute($@"USE ThreeArriveAction
                                  EXEC sp_addextendedproperty @name = N'MS_Description',
                                                                 @value = N'{describe}',
                                                                 @level0type = N'user',
                                                                 @level0name = N'dbo',
                                                                 @level1type = N'table',
                                                                 @level1name = N'{name}';");
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
            var tableList = DbClient.Query<TableList>($@"USE {label}
                                                      SELECT  TableName = CASE WHEN a.colorder=1 THEN d.name
                                                                          ELSE ''
                                                                     END,TableDescribe = CASE WHEN a.colorder=1 THEN ISNULL(f.value,'')
                                                                                    ELSE ''
                                                                               END
                                                        FROM    syscolumns a
                                                        LEFT   JOIN systypes b
                                                                ON a.xusertype=b.xusertype
                                                        INNER   JOIN sysobjects d
                                                                ON a.id=d.id
                                                                   AND d.xtype='U'
                                                                   AND d.name<>'dtproperties'
                                                        LEFT   JOIN syscomments e
                                                                ON a.cdefault=e.id
                                                        LEFT   JOIN sys.extended_properties g
                                                                ON a.id=g.major_id
                                                                   AND a.colid=g.minor_id
                                                        LEFT   JOIN sys.extended_properties f
                                                                ON d.id=f.major_id
                                                                   AND f.minor_id=0 
                                                        ORDER   BY a.id,a.colorder;").ToList();
            var nodeList = tableList.Where(x => !string.IsNullOrEmpty(x.TableName.Trim())).Select(x => new Node
            {
                id = Convert.ToInt32(id + tableList.IndexOf(x)),
                label = $"{x.TableName}{(string.IsNullOrEmpty(x.TableDescribe) ? "" : $"({x.TableDescribe})")}",
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

        public List<Node> Operate = new List<Node>
                                    {
                                        
                                    };

        private object _fieldType = new
        {

        };

        public bool IsReusable => false;

    }

    /// <summary>
    /// 表的 列表信息 包含表名和表描述
    /// </summary>
    public class TableList
    {
        public string TableName { get; set; } = string.Empty;
        public string TableDescribe { get; set; } = string.Empty;
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
        // ReSharper disable once InconsistentNaming
        public string tableName { get; set; } = string.Empty;
        // ReSharper disable once InconsistentNaming
        public string tableDescribe { get; set; } = string.Empty;
        // ReSharper disable once InconsistentNaming
        public string newfield { get; set; } = string.Empty;
        // ReSharper disable once InconsistentNaming
        public string fieldname { get; set; } = string.Empty;
        // ReSharper disable once InconsistentNaming
        public string identifying { get; set; } = string.Empty;
        // ReSharper disable once InconsistentNaming
        public string primarykey { get; set; } = string.Empty;
        // ReSharper disable once InconsistentNaming
        public string types { get; set; } = string.Empty;
        // ReSharper disable once InconsistentNaming
        public string lengths { get; set; } = string.Empty;
        // ReSharper disable once InconsistentNaming
        public string ornull { get; set; } = string.Empty;
        // ReSharper disable once InconsistentNaming
        public string defaults { get; set; } = string.Empty;
        // ReSharper disable once InconsistentNaming
        public string describe { get; set; } = string.Empty;
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