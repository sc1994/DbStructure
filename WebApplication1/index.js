var vm = new Vue({
    el: "#app",
    data: {
        filterText: '', // 表名筛选文本
        editableTabsValue: '0',
        tabIndex: 0,
        editableTabs: [],
        dataTree: [],
        dialogFieldVisible: false,
        formLabelWidth: '130px',
        fieldInfo: {
            fieldname: '',
            newfield: '',
            identifying: false,
            primarykey: false,
            types: '',
            ornull: false,
            defaults: '',
            describe: ''
        },
        btnload: {
            state: false,
            text: '提交'
        },
        fieldTypeOptions: [],
        yetOpenTables: []
    },
    watch: {
        filterText(val) { // 筛选规则
            this.$refs.tree2.filter(val);
            if (!val) {
                this.closeAllTree(vm.$refs.tree2.$children);
            }
        }
    },
    methods: {
        editTableDescribe(tableName, tableDescribe) {
            var that = this;
            var tab = _.find(that.editableTabs, function (e) {
                return e.name === that.editableTabsValue;
            });
            $.ajax({
                type: "post",
                url: "GetInfo.ashx",
                data: {
                    ajaxName: "EditTableDescribe",
                    tableName: tableName,
                    tableDescribe: tableDescribe,
                    dbName: tab.parenttitle
                },
                complete: function () {
                    that.$message('表描述更新成功, 请勿重复操作');
                    // 回写到树结构的label
                    that.yetOpenTables.forEach((table) => {
                        if (table.label.indexOf(tableName) > -1)
                            table.label = `${tableName}(${tableDescribe})`;
                    });
                }
            });
        },
        closeTher() { // 关闭其他
            var that = this;
            var tab = _.find(that.editableTabs, function (e) {
                return e.name === that.editableTabsValue;
            });
            if (tab) {
                that.editableTabs = [tab];
            }
        },
        closeAll() { // 关闭全部
            this.editableTabs = [];
        },
        closeAllTree(treeList) { // 折叠全部树结构
            treeList.forEach((tree) => {
                if (tree.node && tree.node.childNodes && tree.node.childNodes.length > 0 &&
                    tree.node.expanded) {
                    tree.node.expanded = false;
                    this.closeAllTree(tree.node.childNodes);
                } else {
                    if (tree.childNodes && tree.childNodes.length > 0 && tree.expanded) {
                        tree.expanded = false;
                        this.closeAllTree(tree.childNodes);
                    }
                }
            });
        },
        selectrow(row) { // 双击选中一行 ,以供修改字段属性
            this.dialogFieldVisible = true;
            this.fieldInfo = {
                newfield: row.fieldname,
                fieldname: row.fieldname,
                identifying: row.identifying && row.identifying.length > 0,
                primarykey: row.primarykey && row.primarykey.length > 0,
                ornull: row.ornull && row.ornull.length > 0,
                defaults: row.defaults,
                describe: row.describe,
                lengths: row.length,
                types: row.types
            };
        },
        submitrow() { // 提交修改后的字段属性 //todo
            var that = this;
            $.ajax({
                type: "post",
                url: "GetInfo.ashx",
                data: {
                    ajaxName: "SubmitRow",
                    rowData: JSON.stringify(that.fieldInfo)
                },
                complete: function (d) {
                    //d.responseText
                }
            });
            this.dialogFieldVisible = false;
        },
        loadtableinfo(data, node) { // 获取表信息
            var that = this;
            if (node.level === 2) {
                var ishaving = _.findIndex(this.editableTabs, function (tab) {
                    return tab.title === data.label.split("(")[0] && tab.parenttitle === node.parent.label;
                });
                if (ishaving !== -1) {
                    this.editableTabsValue = this.editableTabs[ishaving].name;
                    return;
                }
                var tableName, tableDescribe;
                if (data.label.indexOf("(") > -1) {
                    tableName = data.label.split("(")[0];
                    tableDescribe = data.label.split("(")[1].replace(")", "");
                } else {
                    tableName = data.label;
                    tableDescribe = "";
                }
                $.ajax({
                    type: "post",
                    url: "GetInfo.ashx",
                    data: {
                        "ajaxName": "GetTableInfo",
                        "parentLabel": node.parent.label,
                        "label": tableName
                    },
                    success: function (d) {
                        let newTabName = ++that.tabIndex + '';
                        that.editableTabs.push({
                            title: data.label.split("(")[0],
                            parenttitle: node.parent.label,
                            name: newTabName,
                            tableData: JSON.parse(d),
                            tableName: tableName,
                            tableDescribe: tableDescribe
                        });
                        that.editableTabsValue = newTabName;
                        that.yetOpenTables.push(data);
                    }
                });
                return;
            }
        },
        loadchile(data, node) {
            if (node.level > 1) {
                return;
            }
            if (node.childNodes.length > 1) {
                return;
            }
            $.ajax({
                type: "post",
                url: "GetInfo.ashx",
                data: {
                    "ajaxName": "GetTableList",
                    "id": data.id,
                    "label": data.label
                },
                success: function (d) {
                    data.children = JSON.parse(d);
                }
            });
        },
        filterNode(value, data) { // 筛选规则
            if (!value) {
                return true;
            };
            return data.label.toLowerCase().indexOf(value.toLowerCase()) !== -1;
        },
        handleTabsEdit(targetName, action) {
            if (action === 'remove') {
                let tabs = this.editableTabs;
                let activeName = this.editableTabsValue;
                if (activeName === targetName) {
                    tabs.forEach((tab, index) => {
                        if (tab.name === targetName) {
                            let nextTab = tabs[index + 1] || tabs[index - 1];
                            if (nextTab) {
                                activeName = nextTab.name;
                            }
                        }
                    });
                }
                this.editableTabsValue = activeName;
                this.editableTabs = tabs.filter(tab => tab.name !== targetName);
            }
        }
    },
    mounted() {
        var that = this;
        $.ajax({
            type: "post",
            url: "GetInfo.ashx",
            data: {
                "ajaxName": "GetDbList"
            },
            success: function (d) {
                that.dataTree = JSON.parse(d);
            }
        });
    }
});