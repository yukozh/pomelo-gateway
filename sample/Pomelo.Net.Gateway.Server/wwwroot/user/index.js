component.mounted = function () {
    this.$parent.active = 'users';
};

component.created = function () {
    this.loadUsers();
};

component.data = function () {
    return {
        users: [],
        usersToRemove: [],
        selected: null
    };
};

component.methods = {
    isDirty: function () {
        for (var i = 0; i < this.users.length; ++i) {
            if (this.isRowDirty(this.users[i])) {
                return true;
            }
        }
        return false;
    },
    isRowDirty: function (user) {
        var keys = Object.getOwnPropertyNames(user).filter(x => x.indexOf('__') < 0);
        for (var j = 0; j < keys.length; ++j) {
            if (user[keys[j]] != user['__' + keys[j]]) {
                return true;
            }
        }
        return false;
    },
    revert: function () {
        this.loadUsers();
    },
    loadUsers: async function () {
        this.usersToRemove = [];
        var users = (await qv.get('/api/user')).data; 
        for (var i = 0; i < users.length; ++i) {
            var keys = Object.getOwnPropertyNames(users[i]).filter(x => x.indexOf('__') < 0);
            for (var j = 0; j < keys.length; ++j) {
                users[i]['__' + keys[j]] = users[i][keys[j]];
            }
        }
        this.users = users;
    },
    deleteUser: function (i) {
        if (this.selected == null) {
            return;
        }
        this.usersToRemove.push(i);
        this.selected = null;
    },
    save: async function () {
        for (var i = 0; i < this.usersToRemove.length; ++i) {
            await qv.delete('/api/user/' + this.usersToRemove[i]);
        }
        for (var i = 0; i < this.users.length; ++i) {
            if (this.isRowDirty(this.users[i])) {
                await qv.put('/api/user/' + this.users[i].username, this.users[i]);
            }
        }
        this.revert();
    }
};