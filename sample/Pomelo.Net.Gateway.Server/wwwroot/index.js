var app = new Vue({
    data: {
        token: null,
        user: null,
    },
    mounted: async function () {
    },
    created: function () {
    },
    methods: {
    }
});

var mainContainer = new PomeloComponentContainer('#content', app, app, function (view) {
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