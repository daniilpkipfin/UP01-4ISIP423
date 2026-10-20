IF DB_ID(N'DB_Popov_Kindergartens') IS NULL
    CREATE DATABASE DB_Popov_Kindergartens;
GO
USE DB_Popov_Kindergartens;
GO
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.Users',N'U') IS NULL AND OBJECT_ID(N'dbo.Kindergartens',N'U') IS NULL AND OBJECT_ID(N'dbo.Children',N'U') IS NULL
BEGIN
    BEGIN TRANSACTION;
    CREATE TABLE dbo.Users (
        Id int IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
        Login nvarchar(50) COLLATE Cyrillic_General_100_CI_AS NOT NULL CONSTRAINT UQ_Users_Login UNIQUE,
        FullName nvarchar(150) NOT NULL,
        PasswordHash nvarchar(200) NOT NULL,
        Role varchar(5) NOT NULL CONSTRAINT DF_Users_Role DEFAULT 'User',
        Photo varbinary(max) NULL,
        CONSTRAINT CK_Users_Role CHECK (Role IN ('Admin','User')),
        CONSTRAINT CK_Users_Login CHECK (LEN(Login) BETWEEN 3 AND 50),
        CONSTRAINT CK_Users_Name CHECK (LEN(LTRIM(RTRIM(FullName))) BETWEEN 3 AND 150),
        CONSTRAINT CK_Users_Hash CHECK (PasswordHash LIKE 'PBKDF2-SHA256$%'),
        CONSTRAINT CK_Users_Photo CHECK (Photo IS NULL OR DATALENGTH(Photo)<=2097152)
    );
    CREATE TABLE dbo.Kindergartens (
        Id int IDENTITY(1,1) CONSTRAINT PK_Kindergartens PRIMARY KEY,
        Name nvarchar(100) NOT NULL,
        Address nvarchar(200) NOT NULL,
        Phone varchar(12) NOT NULL,
        Capacity int NOT NULL,
        CONSTRAINT UQ_Kindergartens_Name_Address UNIQUE (Name,Address),
        CONSTRAINT CK_Kindergartens_Name CHECK (LEN(LTRIM(RTRIM(Name)))>0),
        CONSTRAINT CK_Kindergartens_Address CHECK (LEN(LTRIM(RTRIM(Address)))>0),
        CONSTRAINT CK_Kindergartens_Phone CHECK (LEN(Phone)=12 AND LEFT(Phone,2)='+7' AND SUBSTRING(Phone,3,10) NOT LIKE '%[^0-9]%'),
        CONSTRAINT CK_Kindergartens_Capacity CHECK (Capacity BETWEEN 1 AND 500)
    );
    CREATE TABLE dbo.Children (
        Id int IDENTITY(1,1) CONSTRAINT PK_Children PRIMARY KEY,
        KindergartenId int NOT NULL,
        ParentId int NOT NULL,
        FullName nvarchar(150) NOT NULL,
        BirthDate date NOT NULL,
        EnrollmentDate date NOT NULL,
        GroupName nvarchar(50) NOT NULL,
        Status nvarchar(20) NOT NULL,
        MonthlyFee decimal(10,2) NOT NULL,
        CONSTRAINT FK_Children_Kindergartens FOREIGN KEY (KindergartenId) REFERENCES dbo.Kindergartens(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Children_Users FOREIGN KEY (ParentId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
        CONSTRAINT CK_Children_Name CHECK (LEN(LTRIM(RTRIM(FullName)))>0),
        CONSTRAINT CK_Children_Birth CHECK (BirthDate>='2010-01-01' AND BirthDate<=CONVERT(date,GETDATE())),
        CONSTRAINT CK_Children_Enrollment CHECK (EnrollmentDate>=BirthDate AND EnrollmentDate<=CONVERT(date,GETDATE())),
        CONSTRAINT CK_Children_Group CHECK (LEN(LTRIM(RTRIM(GroupName)))>0),
        CONSTRAINT CK_Children_Status CHECK (Status IN (N'Зачислен',N'Ожидает',N'Выпущен')),
        CONSTRAINT CK_Children_Fee CHECK (MonthlyFee BETWEEN 0 AND 100000),
        CONSTRAINT UQ_Children_Identity UNIQUE(ParentId,FullName,BirthDate)
    );
    CREATE INDEX IX_Children_Kindergarten_Status ON dbo.Children(KindergartenId,Status);
    CREATE INDEX IX_Children_Parent ON dbo.Children(ParentId);
    INSERT dbo.Users(Login,FullName,PasswordHash,Role) VALUES
    (N'admin',N'Попов Даниил Станиславович',N'PBKDF2-SHA256$210000$HZjApbxM05Fi6F+Zs16Ubw==$sC6LCms3a7D3jDRHKKFJwGlKIG74wietyYdRUtHnmWc=','Admin'),
    (N'parent',N'Иванова Анна Сергеевна',N'PBKDF2-SHA256$210000$CoJXQFCN2VFsqwiObeFhtg==$0H7zwqxH/QFQtFCyzPSv1NhSPP01xKaszDu9Mq8uoGU=','User'),
    (N'parent2',N'Петров Алексей Игоревич',N'PBKDF2-SHA256$210000$miIlHMXXawNldx5gq/BpvA==$vpLH7ul7CFAcrW8btbd30NQ4HKTU6VwLssp/fpI81go=','User'),
    (N'parent3',N'Сидорова Мария Андреевна',N'PBKDF2-SHA256$210000$ezitJCF26c7Hy/sIsEo4/g==$XdgkDOfGZophz4L5M8LJWpIcqDWLuYCUpgyVdA1cphk=','User'),
    (N'parent4',N'Смирнов Павел Олегович',N'PBKDF2-SHA256$210000$OHBt9Phiqr6fcCIpY8w0wg==$WKl1leo5cIFQGUiHdTikRKhxhebXThYvFqrWHpz6jYY=','User');
    INSERT dbo.Kindergartens(Name,Address,Phone,Capacity) VALUES
    (N'Солнышко',N'Учебный адрес 1','+74950000001',30),
    (N'Радуга',N'Учебный адрес 2','+74950000002',25),
    (N'Ромашка',N'Учебный адрес 3','+74950000003',20);
    INSERT dbo.Children(KindergartenId,ParentId,FullName,BirthDate,EnrollmentDate,GroupName,Status,MonthlyFee) VALUES
    (1,2,N'Иванов Михаил Сергеевич','2021-03-15','2026-09-01',N'Старшая',N'Зачислен',4800),
    (1,3,N'Петров Артем Алексеевич','2022-06-21','2026-09-01',N'Средняя',N'Зачислен',4500),
    (1,4,N'Сидорова София Андреевна','2023-02-11','2026-09-01',N'Младшая',N'Зачислен',4200),
    (1,5,N'Смирнов Максим Павлович','2021-11-08','2026-09-01',N'Старшая',N'Зачислен',4800),
    (2,2,N'Иванова Алиса Сергеевна','2023-05-18','2026-09-01',N'Младшая',N'Зачислен',4300),
    (2,3,N'Петрова Ева Алексеевна','2021-08-30','2026-09-01',N'Старшая',N'Зачислен',4900),
    (2,4,N'Сидоров Иван Андреевич','2022-09-02','2026-09-01',N'Средняя',N'Зачислен',4600),
    (2,5,N'Смирнова Полина Павловна','2023-12-09','2026-09-01',N'Младшая',N'Ожидает',4300),
    (3,2,N'Иванов Кирилл Сергеевич','2020-07-12','2026-09-01',N'Подготовительная',N'Зачислен',5000),
    (3,3,N'Петров Матвей Алексеевич','2022-04-03','2026-09-01',N'Средняя',N'Зачислен',4400),
    (3,4,N'Сидорова Виктория Андреевна','2021-01-25','2026-09-01',N'Старшая',N'Зачислен',4700),
    (3,5,N'Смирнов Александр Павлович','2019-10-17','2025-09-01',N'Подготовительная',N'Выпущен',5000);
    COMMIT TRANSACTION;
END;
GO
IF OBJECT_ID(N'dbo.Users',N'U') IS NULL OR OBJECT_ID(N'dbo.Kindergartens',N'U') IS NULL OR OBJECT_ID(N'dbo.Children',N'U') IS NULL
    THROW 50001,N'База имеет неполную структуру. Автоматическое изменение существующих таблиц отменено.',1;
GO
