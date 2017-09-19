using System;
using System.Web;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace WebApplication1
{
    public class GetInfo : IHttpHandler
    {
        #region 私属性
        private List<Node> Operate { get; } = new List<Node>
                                             {
                                                 new Node
                                                 {
                                                     label = "数据展示"
                                                 }
                                             };

        // 数据类型 用于扩展
        //private object _fieldType = new
        //{

        //}; 
        #endregion

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

        #region 编辑表描述
        /// <summary>
        /// 编辑表描述
        /// </summary>
        /// <param name="context"></param>
        public void EditTableDescribe(HttpContext context)
        {
            var name = context.Request["tableName"];
            var describe = context.Request["tableDescribe"];
            var dbName = context.Request["dbName"];
            // todo 因为无法判断当前是否已有描述 出此下策 目前是先执行更新出现异常再尝试添加
            try
            {
                // 执行 更新
                DbClient.Excute($@"USE {dbName}
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
                DbClient.Excute($@"USE {dbName}
                                  EXEC sp_addextendedproperty @name = N'MS_Description',
                                                                 @value = N'{describe}',
                                                                 @level0type = N'user',
                                                                 @level0name = N'dbo',
                                                                 @level1type = N'table',
                                                                 @level1name = N'{name}';");
            }

        } 
        #endregion

        #region 编辑字段信息 todo 暂未实现
        /// <summary>
        /// 编辑字段信息 todo 暂未实现
        /// </summary>
        /// <param name="context"></param>
        public void SubmitRow(HttpContext context)
        {
            var nodeList = JsonConvert.DeserializeObject<TableInfo>(context.Request["rowData"]);

            context.Response.Write(JsonConvert.SerializeObject(nodeList));
            context.Response.End();
        }
        #endregion

        #region 获取全部的库
        /// <summary>
        /// 获取全部的库
        /// </summary>
        /// <param name="context"></param>
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
        #endregion

        #region 获取库下全部表
		/// <summary>
        /// 获取库下全部表
        /// </summary>
        /// <param name="context"></param>
        public void GetTableList(HttpContext context)
        {
            var id = context.Request["id"];
            var label = context.Request["label"];
            var tableList = DbClient.Query<TableBaseInfo>($@"USE {label}
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
        #endregion

        #region 获取表详细信息
        /// <summary>
        /// 获取表详细信息
        /// </summary>
        /// <param name="context"></param>
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
                defaults = x.defaults.Replace("(", "").Replace(")", "").Replace("'", ""),
                primarykey = x.primarykey,
                ornull = x.ornull,
                types = x.types.ToLower().Contains("char") ? $"{x.types}({x.lengths})" : x.types,
            })));
            context.Response.End();
        } 
        #endregion

        public bool IsReusable => false;
    }
}