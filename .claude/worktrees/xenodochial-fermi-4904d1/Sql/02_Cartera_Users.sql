CREATE TABLE IF NOT EXISTS Roles (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Users (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Username VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    RoleId INT NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);

INSERT IGNORE INTO Roles (Name) VALUES ('Admin'), ('User');
-- Password is 'admin123' hashed with SHA256 (for simplicity in custom auth, or we can use BCrypt via C#)
-- We will insert user from the app or provide a default one.

CREATE TABLE IF NOT EXISTS CarteraClientes (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Nombre VARCHAR(150) NOT NULL,
    CompaniaSeguros VARCHAR(100),
    Ramo VARCHAR(50),
    Cuotas INT,
    FormaPago VARCHAR(50),
    Poliza VARCHAR(100),
    Certificado VARCHAR(100),
    Endoso VARCHAR(100),
    PrimaNeta DECIMAL(18,2),
    SeguroAsiento DECIMAL(18,2),
    PrimaComercial DECIMAL(18,2),
    Impuesto DECIMAL(18,2),
    GastosEmision DECIMAL(18,2),
    Bomberos DECIMAL(18,2),
    PrimaTotal DECIMAL(18,2),
    Plan VARCHAR(100),
    SumaAsegurada VARCHAR(100),
    Vigencia DATE,
    Hasta DATE,
    Medio VARCHAR(100),
    Vehiculo VARCHAR(100),
    Contacto VARCHAR(50),
    Correo VARCHAR(100),
    Observaciones TEXT,
    Cumpleanos DATE,
    EmisionRenovacion VARCHAR(50),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
