using System.Collections.Generic;

namespace WebApplication1
{
    /// <summary>
    /// 节点
    /// </summary>
    public class Node
    {
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
}