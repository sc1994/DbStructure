# DbStructure
查看SQL Server 数据库结构 web 页面

## 写在前面
为什么想写个这个...
- 当然是因为 在 SQL Server Management 中查看表结构是件痛苦的事情 (还有每步的操作都是那么的慢)
- 还有就是当我们加入一个已经开始开发了的项目组,且此组没有数据库文档的时候你就知道这个有多好用了 - -, 别问我为什么知道的

### 运行
- clone项目
- 编译 
- 挂载IIS 
- 别忘记修改web.config
- 然后访问就可以啦

### 功能
- 查看web.config指定链接的数据库列表,以及每个库中的表,表中的字段,
- 表信息包含(字段名/值类型/描述/主键/自增键/可空/默认值) 
- 维护表描述 如  UserTable(用户信息表)

### 技术栈
没啥难的 
vue + element ui

### 后续 
- 目标就是能方便我们的快速开发, 所以会加上一些基本的查询,以及更新数据的功能
- 多人团队可能就需要登陆以及操作日志等功能了
