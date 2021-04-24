var app = new Vue({
    data: {
        token: null,
        user: null,
        notifications: []
    },
    mounted: async function () {
        app.$container.open('/menu');
        setInterval(function () {
            for (var i = 0; i < app.notifications.length; ++i) {
                if (app.notifications[i].closeTime > 0) {
                    --app.notifications[i].closeTime;
                }
                if (app.notifications[i].closeTime == 0) {
                    app.notifications.splice(i, 1);
                }
            }
        }, 1000);
    },
    methods: {
        newGuid: function () {
            return 'xxxxxxxx-xxxx-xxxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
                var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
                return v.toString(16);
            });
        },
        notify: function (title, subtitle, color, closeTime, id) {
            var generateId = !id;
            id = id || this.newGuid();
            if (generateId) {
                this.notifications.push({ title: title, subtitle: subtitle, color: color, closeTime: closeTime, id: id });
            } else {
                var notification = this.notifications.filter(x => x.id == id);
                if (!notification.length) {
                    return null;
                }
                notification = notification[0];
                notification.title = title;
                notification.subtitle = subtitle;
                notification.color = color;
                notification.closeTime = closeTime;
            }
            return id;
        },
        closeNotification: function (id) {
            for (var i = 0; i < this.notifications.length; ++i) {
                if (this.notifications[i].id == id) {
                    this.notifications.splice(i, 1);
                    return;
                }
            }
        }
    }
});

var mainContainer = new PomeloComponentContainer('#main', app, app, function (view) {
}, function () { });

app.$container = mainContainer;
app.$mount('#app');

window.onpopstate = function (event) {
    if (app.$container.active) {
        app.$container.close(app.$container.active);
    }
    if (window.location.pathname !== '/') {
        app.open(window.location.pathname + window.location.search);
    }
};

$(window).click(function (e) {
    if ($(e.target).attr('vue-route') !== undefined) {
        let href = $(e.target).attr('href');
        window.history.pushState(null, null, href);
        app.open(href);
        e.preventDefault();
    }
    if ($(e.target).parents('[vue-route]').length > 0) {
        let href = $(e.target).parents('[vue-route]').attr('href');
        window.history.pushState(null, null, href);
        app.open(href);
        e.preventDefault();
    }
});

function sleep(ms) {
    return new Promise((res, rej) => {
        setTimeout(() => res());
    });
}