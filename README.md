## This is not the official System.Data.SQLite

The official ADO.NET wrapper for [SQLite](http://sqlite.org/) can be found at [http://system.data.sqlite.org/](http://system.data.sqlite.org/).

This is an independent implementation of the core of ADO.NET: `IDbConnection`, `IDbCommand`, `IDbDataReader`, `IDbTransaction` (plus a few helpers) -- enough types to let you create and query SQLite databases from managed code, including support for libraries such as [Dapper](https://code.google.com/p/dapper-dot-net/).

## Why?

There are several problems with the official SQLite wrapper that prompted this replacement:

1. Not Invented Here :smile:
2. Has performance problems, such as the `SQLiteFunction` static constructor that scans all loaded assemblies for usages of `SQLiteFunctionAttribute`.
3. Has complicated finalizers to work around [incorrect](http://system.data.sqlite.org/index.html/tktview?name=6734c27589) [use](http://system.data.sqlite.org/index.html/info/6434e23a0f); this library instead just requires that `Dispose` be called on all `IDisposable` objects. (These finalizers were in [Robert Simpson's version](http://system.data.sqlite.org/index.html/fdiff?v1=48351463bded6f9f&v2=771e0c8865d4b055&patch); they may have been removed in later versions.)
4. Has interesting [type conversion](https://github.com/LogosBible/System.Data.SQLite/wiki/Type-conversion) behavior.
5. Introduces [breaking changes](http://system.data.sqlite.org/index.html/tktview?name=1c456ae75f).
6. Doesn't support passing a VFS name to `sqlite3_open_v2`.
7. Builds platform-specific assemblies, instead of Any CPU ones.
8. If we maintain our own fork to work around these changes, there are frequent conflicts when merging in new upstream versions.

There are also improvements we can make in a custom wrapper:

* `DbDataReader.ReadAsync(CancellationToken)` is overridden to support cancellation from another thread (via [`sqlite_progress_handler`](http://www.sqlite.org/c3ref/progress_handler.html)).

## Notes

This library is generally compatible with the official System.Data.SQLite 
API, but a few changes were made where necessary:

* [SQLiteConnectionStringBuilder](https://github.com/LogosBible/System.Data.SQLite/blob/master/src/System.Data.SQLite/SQLiteConnectionStringBuilder.cs) does not support all the official connection string properties.
* [SQLiteDataReader](https://github.com/LogosBible/System.Data.SQLite/blob/master/src/System.Data.SQLite/SQLiteDataReader.cs) performs fewer implicit conversions.
* Not all SQL type aliases (`text`, `int`, `blob`, etc.) are supported.

This wrapper is managed-only; you still need a copy of the native SQLite library. A recent copy is provided in the `lib` folder (for the unit tests).