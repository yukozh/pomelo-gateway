component.data = function () {
    return {
        active: null
    };
};

component.created = function () {
    this.$container = new PomeloComponentContainer('#content', app, this, function (view) {
    }, function () { });
    if (window.location.pathname != '/') {
        this.open(window.location.pathname + window.location.search);
    }
};

component.methods = {
    open: async function (url, pushState = true) {
        if (this.$container.active) {
            this.$container.close(this.$container.active);
        }

        var splited = url.split('?');
        var _params = {};
        if (splited.length > 1) {
            var params = splited[1].split('&');
            for (let i = 0; i < params.length; ++i) {
                let _splited = params[i].split('=');
                _params[_splited[0]] = decodeURIComponent(_splited[1]);
            }
        }
        if (this.$container.active) {
            this.$container.close(this.$container.active);
        }
        this.child = await this.$container.open(splited[0], _params);
        if (pushState) {
            window.history.pushState(null, null, url);
        }
    }
};