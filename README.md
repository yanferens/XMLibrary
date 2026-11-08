# XMLLibrary — кафедральна бібліотека

Лабораторна робота 2 / 2\*: пошук у XML трьома способами (SAX, DOM, LINQ to XML), перетворення XSLT → HTML,
Google Drive, патерни «Стратегія», «Фабричний метод», «Одинак» і журнал подій.

## Як запустити

1. Відкрийте `XMLLibrary.sln` у Visual Studio 2022 (потрібні .NET 9 і робоче навантаження **.NET MAUI**).
2. Зробіть `XMLLibrary` стартовим проєктом, ціль — **Windows Machine**.
3. **Build → Rebuild Solution**, потім **F5**.
4. Натисніть «З диска» в картці XML і відкрийте `Samples/library.xml`, у картці XSL — `Samples/library.xsl`.

Тести: **Test → Run All Tests** (проєкт `XMLLibrary.Tests`, xUnit).

## Google Drive

Програмі потрібен OAuth-клієнт Google:

1. [Google Cloud Console](https://console.cloud.google.com/) → створіть проєкт → **APIs & Services → Library** → увімкніть **Google Drive API**.
2. **OAuth consent screen**: тип *External*, додайте свою пошту в **Test users**.
3. **Credentials → Create credentials → OAuth client ID**, тип **Desktop app** → **Download JSON**.
4. Перейменуйте файл на `client_secret.json` і покладіть у `XMLLibrary/Resources/Raw/`. Можна взяти той самий файл, що й у BoolSheet.
5. Перезберіть програму. Перша дія з Drive відкриє вхід у браузері; далі вхід запам'ятовується.

`client_secret.json` вказано в `.gitignore` — не комітьте його.

## Структура

| Проєкт | Що всередині |
|---|---|
| `XMLLibrary.Core` | Уся логіка без інтерфейсу: моделі, три стратегії пошуку, XSLT, збереження, журнал, інтерфейс хмари |
| `XMLLibrary` | Застосунок .NET MAUI: сторінка, view model, Google Drive, вибір файлів |
| `XMLLibrary.Tests` | Тести ядра та view model без вікна |
| `Samples` | Приклад `library.xml` і шаблон `library.xsl` |

## Де що з умови

| Вимога | Де в коді |
|---|---|
| XML з різною кількістю атрибутів, 2 рівні | `Samples/library.xml` (`book` → `readers/reader`) |
| SAX / DOM / LINQ to XML | `Core/Search/SaxSearchStrategy.cs`, `DomSearchStrategy.cs`, `LinqSearchStrategy.cs` |
| «Стратегія» | `IXmlSearchStrategy` + перемикач SAX / DOM / LINQ у заголовку вікна |
| Динамічна генерація запитів | `DomSearchStrategy.BuildXPath` (XPath), `LinqSearchStrategy.Search` (ланцюжок `Where`) |
| Значення атрибутів з файлу («підгрузка даних») | `Core/Model/AttributeCatalog.cs` → випадні списки |
| Пошук за ключовими словами | `SearchCriteria.MatchesKeyword` (без урахування регістру, кілька слів) |
| XSLT → HTML | `Core/Transform/XslTransformer.cs`, кнопка «Перегляд HTML» |
| Clear | кнопка «Очистити» → `MainViewModel.ClearAsync` |
| Вихід «Так / Ні» (кнопка й хрестик) | `MainViewModel.ExitAsync`, `MauiProgram.OnWindowClosing` |
| Без «магічної кнопки» | кнопки прив'язані до команд `MainViewModel`, уся логіка в `XMLLibrary.Core` |
| Google Drive: відкрити XML/XSL, зберегти фрагмент | `Cloud/GoogleDriveStorage.cs` (Адаптер над Google API) |
| «Фабричний метод» | `Core/Export/FragmentSaver.cs`: творці `XmlFragmentSaver`, `HtmlFragmentSaver`; продукти `XmlFragmentWriter`, `HtmlFragmentWriter` |
| «Одинак» — журнал подій | `Core/Logging/EventLogger.cs`, файл `%LOCALAPPDATA%\XMLLibrary\events.log`, кнопка «Журнал подій» |

## Надійність

- Будь-яка помилка (зламаний XML/XSL, немає мережі, не той файл) показується повідомленням, програма не падає.
- Поки триває операція, інші кнопки вимкнено: дію не можна запустити двічі.
- Пошук і перетворення виконуються у фоні, тож вікно не зависає на великому файлі.
- DTD заборонено, XSLT без `document()` і скриптів: чужий файл не прочитає ваші файли.
- Підтримуються файли в `windows-1251`, значення з апострофами та лапками, порожні елементи, CDATA.
- Непередбачені винятки записуються в `%LOCALAPPDATA%\XMLLibrary\crash.log`.
