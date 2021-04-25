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
        selected: null,
        ui: {
            createUser: false,
            resetPassword: false
        },
        createUser: {
            validateFields: false,
            form: null
        },
        resetPassword: {
            validateFields: false,
            password: ''
        }
    };
};

component.watch = {
    deep: true,
    'ui.createUser': function () {
        if (this.ui.createUser) {
            this.createUser.form = {
                username: '',
                password: '',
                role: 'User',
                allowCreateOnDemandEndpoint: false
            };
            this.createUser.validateFields = false;
        }
    }
};

component.methods = {
    close: function () {
        this.$parent.active = null;
        this.$parent.$container.close(this);
    },
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
                await qv.put('/api/user/' + this.users[i].username, {
                    username: this.users[i].username,
                    role: this.users[i].role,
                    allowCreateOnDemandEndpoint: this.users[i].allowCreateOnDemandEndpoint
                });
            }
        }
        this.revert();
    },
    createUserClick: async function () {
        var nId = app.notify('Creating User', `Validating arguments...`, 'blue', -1);
        this.createUser.validateFields = true;
        await sleep(500);
        if ($('.invalid').length) {
            app.notify('Create Failed', `Arguments are invalid.`, 'red', 3, nId);
            return;
        }
        app.notify('Creating User', `Creating ${this.createUser.form.username}`, 'blue', -1, nId);
        try {
            await qv.post('/api/user', this.createUser.form);
            app.notify('Created User', `Created ${this.createUser.form.username}...`, 'green', 5, nId);
            this.ui.createUser = false;
            this.loadUsers();
        } catch (err) {
            app.notify('Create Failed', err.responseJSON.message, 'red', 10, nId);
        }
    },
    showResetPassword: function (user) {
        if (!user) {
            this.ui.resetPassword = false;
            return;
        }
        this.ui.resetPassword = !this.ui.resetPassword;
        if (this.ui.resetPassword) {
            this.resetPassword.validateFields = false;
            this.resetPassword.password = '';
        }
    },
    resetPasswordClick: async function () {
        var nId = app.notify('Reset Password', `Validating arguments...`, 'blue', -1);
        this.resetPassword.validateFields = true;
        await sleep(500);
        if ($('.invalid').length) {
            app.notify('Reset Password', `Arguments are invalid.`, 'red', 3, nId);
            return;
        }
        app.notify('Reset Password', `Restting password ${this.selected}`, 'blue', -1, nId);
        try {
            var user = this.users.filter(x => x.username == this.selected)[0];
            await qv.post('/api/user/' + this.selected, {
                username: user.username,
                password: this.resetPassword.password,
                role: user.__role,
                allowCreateOnDemandEndpoint: user.__allowCreateOnDemandEndpoint
            });
            app.notify('Reset Password', `Succeeded`, 'green', 5, nId);
            this.ui.resetPassword = false;
        } catch (err) {
            app.notify('Reset Password Failed', err.responseJSON.message, 'red', 10, nId);
        }
    }
};