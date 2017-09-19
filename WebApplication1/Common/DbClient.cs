using Dapper;
using System;
using System.Data;
using System.Configuration;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace WebApplication1
{
    public class DbClient
    {
        public static IEnumerable<T> Query<T>(string sql, object param = null)
        {
            if (string.IsNullOrEmpty(sql))
            {
                throw new ArgumentNullException(nameof(sql));
            }
            using (var con = DataSource.GetConnection())
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
            using (var con = DataSource.GetConnection())
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
            using (var con = DataSource.GetConnection())
            {
                return con.ExecuteScalar<T>(sql, param);
            }
        }

        public static T ExecuteScalarProc<T>(string strProcName, object param = null)
        {
            using (var con = DataSource.GetConnection())
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
            using (var con = DataSource.GetConnection())
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
                using (var con = DataSource.GetConnection())
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