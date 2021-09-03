# Version History

## 3.2.0

* `SQLiteLog` only calls `sqlite3_config` to initialize logging when an event handler is attached to `SQLiteLog.Log`.
  * It is no longer called automatically from the `SQLiteConnection` constructor. This fixes a `TypeInitializationException` and `AppDomainUnloadedException` in `SQLiteLog`.
  * If you are using `SQLiteLog.Log`, you _must_ attach an event handler before calling any other methods of this library. This is a [limitation of SQLite](https://www.sqlite.org/c3ref/config.html).
  * If other methods in this library are used before `SQLiteLog.Log`, the native event handler will fail to be installed properly, and no `Log` events will be raised. 

## 3.1.0

* Support `JournalSizeLimit` and `PersistWal` connection string options.

## 3.0.0

* Support [URI filenames](https://www.sqlite.org/uri.html).
* Supported frameworks: `netstandard2.0`, `net472`, `xamarin.ios10`, `monoandroid81`

## 2.13.1

* Add `net47` target platform.

## 2.13.0

* Rewrite `SQLiteConnection.StatementCompleted` to use `sqlite3_trace_v2` API internally: [#29](https://github.com/Faithlife/System.Data.SQLite/pull/29).
  * This requires [SQLite 3.14](https://sqlite.org/releaselog/3_14.html) or later.

## 2.12.3

* Use `DateTimeStyles.AdjustToUniversal`: [#24](https://github.com/Faithlife/System.Data.SQLite/pull/24).

## 2.12.2

* Create more informative error messages in `SQLiteException`.

## 2.12.1

* Fix the native interop signature for `sqlite3_interrupt`.

## 2.12.0

* Sleep in native code when the database is busy. This reduces managed/native interop.
