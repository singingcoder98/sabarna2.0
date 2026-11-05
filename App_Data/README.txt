This folder holds app.db — the entire shop: products, orders, settings.

It lives outside wwwroot on purpose: IIS hands a folder called App_Data write permission
by default, and nothing in here is ever served to a browser. On a shared host the web.config
in the site root points the connection string here:

    Data Source=App_Data\app.db

Back it up: copy app.db somewhere else now and then. Deleting it re-seeds the demo catalogue.
