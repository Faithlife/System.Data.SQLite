# Version History

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