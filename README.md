![build](https://github.com/planara/planara-files/actions/workflows/build.yml/badge.svg)
![release](https://github.com/planara/planara-files/actions/workflows/release.yml/badge.svg)
![publish-k3s](https://github.com/planara/planara-files/actions/workflows/publish-k3s.yml/badge.svg)
![version](https://img.shields.io/github/v/tag/planara/planara-files?sort=semver)
[![Codecov](https://codecov.io/gh/planara/planara-files/branch/main/graph/badge.svg)](https://codecov.io/gh/planara/planara-files)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](http://makeapullrequest.com)

## Planara.Files

Сервис управления пользовательскими файлами.

Отвечает за загрузку, обновление, скачивание и удаление файлов проекта.
Метаданные файлов хранятся в PostgreSQL, а сами файлы сохраняются в S3-compatible object storage через MinIO.

Реализован как ASP.NET Core REST API с JWT-аутентификацией.

## Features

- Загрузка файлов через `multipart/form-data`
- Скачивание файлов по ID
- Обновление содержимого файла
- Мягкое удаление файлов через статус `Deleted`
- Хранение метаданных файлов в PostgreSQL
- Хранение содержимого файлов в MinIO / S3-compatible storage
- Поддержка приватных и публичных файлов
- Проверка владельца файла при доступе
- JWT авторизация (`[Authorize]`)
- Валидация входных параметров через FluentValidation
- Поддержка файлов: `png`, `jpg`, `jpeg`, `webp`, `obj`

## REST API

### Upload

- `POST /api/files/upload`
  Загружает файл пользователя
  -(требует авторизации)-

### Download

- `GET /api/files/{id}/download`
  Скачивает файл по ID
  -(требует авторизации)-

### Update

- `PUT /api/files/{id}`
  Обновляет содержимое файла
  -(требует авторизации)-

### Delete

- `DELETE /api/files/{id}`
  Удаляет файл пользователя
  -(требует авторизации)-