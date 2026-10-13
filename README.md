# VpnMonitor

Мониторинг клиентов OpenVPN-сервера: кто подключён, с какой скоростью качает и когда подключался в последний раз.

## Как это устроено

| Проект | Что делает |
| --- | --- |
| `VpnMonitor.Service` | Консольный сервис на сервере с OpenVPN. Раз в 2 минуты запрашивает `status` у management-интерфейса и пишет снимок по каждому клиенту в PostgreSQL. |
| `VpnMonitor.WUI` | Клиент под Windows (WinUI 3). Читает данные из той же БД и показывает список клиентов. |
| `VpnMonitor.Data` | Общая часть: сущности, `DataContext`, миграции EF Core, расчёт скорости. |
| `VpnMonitor.Tests` | Тесты парсера ответа OpenVPN и расчёта скорости. |

Клиент считается онлайн, если за последние 10 минут у его текущей сессии есть минимум два снимка. Скорость — разница счётчиков байтов между двумя последними снимками.

## Требования

- .NET SDK 8 или новее
- PostgreSQL
- OpenVPN с включённым management-интерфейсом на `127.0.0.1:7505`:

  ```
  management 127.0.0.1 7505
  ```

- Для `VpnMonitor.WUI` — Windows 10 1809+ и Visual Studio 2022 с нагрузкой «Разработка приложений WinUI»

## Настройка

Скопируйте `appsettings.Example.json` под именем `appsettings.Private.json` в папки `VpnMonitor.Service` и `VpnMonitor.WUI` и впишите строку подключения. Файл в git не попадает.

| Параметр | Значение |
| --- | --- |
| `NpgSql:ConnectionString` | Строка подключения к PostgreSQL. |
| `Monitor:RetentionDays` | Сколько дней хранить снимки, по умолчанию 30. `0` отключает очистку. Используется только сервисом. |

Адрес management-интерфейса и интервал опроса заданы в `VpnMonitor.Service/Worker/MonitorWorker.cs`.

## База данных

Схема ведётся миграциями EF Core. Сервис применяет их сам при каждом запуске, поэтому пользователю БД нужны права на создание таблиц и индексов. Если таблица `vpn-client` уже существует (создана до появления миграций), она остаётся как есть.

Команды `dotnet ef` запускаются через проект сервиса: контекст и строка подключения берутся из его DI и `VpnMonitor.Service/appsettings.Private.json`. Один раз после клонирования:

```bash
dotnet tool restore
```

Новая миграция после изменения сущностей:

```bash
dotnet ef migrations add ИмяМиграции --project VpnMonitor.Data --startup-project VpnMonitor.Service
```

Применить миграции вручную, не запуская сервис (к базе из `appsettings.Private.json`):

```bash
dotnet ef database update --project VpnMonitor.Data --startup-project VpnMonitor.Service
```

Раз в сутки сервис удаляет снимки старше `RetentionDays`. Последний снимок каждого клиента сохраняется всегда, чтобы клиент не пропадал из списка и была видна дата его последнего подключения.

## Запуск

Сервис:

```bash
dotnet run --project VpnMonitor.Service
```

Публикация сервиса одним файлом под Linux:

```bash
dotnet publish VpnMonitor.Service -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Рядом с исполняемым файлом должен лежать `appsettings.Private.json`. При потере связи с OpenVPN или БД сервис завершается, поэтому запускать его стоит под systemd с `Restart=always`.

Клиент запускается из Visual Studio (проект `VpnMonitor.WUI`, платформа x64) или из консоли:

```bash
dotnet build VpnMonitor.WUI -p:Platform=x64
```

## Тесты

```bash
dotnet test VpnMonitor.Tests
```
