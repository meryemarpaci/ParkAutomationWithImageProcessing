CREATE DATABASE OtoparkSistemi;
GO

USE OtoparkSistemi;
GO

CREATE TABLE Sirketler (
    SirketID INT IDENTITY(1,1) PRIMARY KEY,
    SirketAdi NVARCHAR(100) NOT NULL,
    SaatlikUcret DECIMAL(10, 2) NOT NULL DEFAULT 50.00,
    OtoparkKapasitesi INT NOT NULL DEFAULT 100
);

GO

CREATE TABLE AracGirisCikis (
    AracID INT PRIMARY KEY IDENTITY(1,1),
    Plaka NVARCHAR(15) NOT NULL,
    GirisTarihi DATETIME NOT NULL,
    CikisTarihi DATETIME NULL,
    SirketID INT NOT NULL,
    FOREIGN KEY (SirketID) REFERENCES Sirketler(SirketID),
    Durum NVARCHAR(50) DEFAULT 'GirisYapildi'
);
GO





-- Kullanicilar tablosu
CREATE TABLE Kullanicilar (
    KullaniciID INT IDENTITY(1,1) PRIMARY KEY,
    AdSoyad NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    Sifre NVARCHAR(255) NOT NULL,
    SirketID INT NOT NULL,
    FOREIGN KEY (SirketID) REFERENCES Sirketler(SirketID)
);
GO

-- YetkiliPlakalar tablosu
CREATE TABLE YetkiliPlakalar (
    YetkiliID INT IDENTITY(1,1) PRIMARY KEY,
    Plaka NVARCHAR(15) NOT NULL,
    SirketID INT NOT NULL,
    FOREIGN KEY (SirketID) REFERENCES Sirketler(SirketID)
);
GO

-- Odemeler tablosu
CREATE TABLE Odemeler (
    OdemeID INT IDENTITY(1,1) PRIMARY KEY,
    AracID INT NOT NULL,
    OdemeTarihi DATETIME NOT NULL DEFAULT GETDATE(),
    OdemeMiktari DECIMAL(10, 2) NOT NULL,
    FOREIGN KEY (AracID) REFERENCES AracGirisCikis(AracID)
);
GO

