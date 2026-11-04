IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE TABLE [CuentasBancarias] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(1000) NOT NULL,
        [Banco] nvarchar(1000) NOT NULL,
        [NumeroCuenta] nvarchar(1000) NOT NULL,
        [Titular] nvarchar(1000) NOT NULL,
        [Moneda] nvarchar(1000) NOT NULL,
        [SaldoInicial] decimal(18,2) NOT NULL,
        [Estado] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CuentasBancarias] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE TABLE [CuentasVentas] (
        [Id] int NOT NULL IDENTITY,
        [NotaVentaId] int NOT NULL,
        [ClienteId] int NOT NULL,
        [AsesorId] int NOT NULL,
        [TipoVenta] nvarchar(1000) NOT NULL,
        [Moneda] nvarchar(1000) NOT NULL,
        [MontoLiquido] decimal(18,2) NOT NULL,
        [CuotaInicial] decimal(18,2) NOT NULL,
        [MontoComision] decimal(18,2) NOT NULL,
        [Estado] nvarchar(1000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_CuentasVentas] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE TABLE [MetodosPago] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(1000) NOT NULL,
        [Estado] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_MetodosPago] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE TABLE [VentasAnuladas] (
        [Id] int NOT NULL IDENTITY,
        [NotaVentaId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_VentasAnuladas] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE TABLE [Egresos] (
        [Id] int NOT NULL IDENTITY,
        [Fecha] date NOT NULL,
        [Concepto] nvarchar(1000) NOT NULL,
        [Categoria] nvarchar(100) NOT NULL,
        [Monto] decimal(18,2) NOT NULL,
        [Moneda] nvarchar(1000) NOT NULL,
        [Origen] nvarchar(1000) NOT NULL,
        [CuentaVentaId] int NULL,
        [AsesorId] int NULL,
        [CuentaBancariaId] int NULL,
        [UserId] int NOT NULL,
        [Beneficiario] nvarchar(1000) NULL,
        [Observaciones] nvarchar(1000) NULL,
        [Estado] nvarchar(1000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Egresos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Egresos_CuentasBancarias_CuentaBancariaId] FOREIGN KEY ([CuentaBancariaId]) REFERENCES [CuentasBancarias] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Egresos_CuentasVentas_CuentaVentaId] FOREIGN KEY ([CuentaVentaId]) REFERENCES [CuentasVentas] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE TABLE [PlanesPago] (
        [Id] int NOT NULL IDENTITY,
        [CuentaVentaId] int NOT NULL,
        [Monto] decimal(18,2) NOT NULL,
        [NumeroCuotas] int NOT NULL,
        [TasaInteres] decimal(18,2) NOT NULL,
        [TasaMora] decimal(18,2) NOT NULL,
        [FechaInicio] date NOT NULL,
        [FechaFinal] date NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PlanesPago] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanesPago_CuentasVentas_CuentaVentaId] FOREIGN KEY ([CuentaVentaId]) REFERENCES [CuentasVentas] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE TABLE [Reprogramaciones] (
        [Id] int NOT NULL IDENTITY,
        [PlanPagoId] int NOT NULL,
        [UsuarioId] int NOT NULL,
        [SolicitudId] uniqueidentifier NOT NULL,
        [Motivo] nvarchar(1000) NOT NULL,
        [FechaReprogramacion] date NOT NULL,
        [CuotaDesde] int NOT NULL,
        [SaldoCapitalMomento] decimal(18,2) NOT NULL,
        [NuevaTasaInteres] decimal(18,2) NOT NULL,
        [NuevoNumeroCuotas] int NOT NULL,
        [NuevaFechaInicio] date NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Reprogramaciones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reprogramaciones_PlanesPago_PlanPagoId] FOREIGN KEY ([PlanPagoId]) REFERENCES [PlanesPago] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE TABLE [Cuotas] (
        [Id] int NOT NULL IDENTITY,
        [PlanPagoId] int NOT NULL,
        [NumeroCuota] int NOT NULL,
        [ReprogramacionId] int NULL,
        [FechaVencimiento] date NOT NULL,
        [MontoCuota] decimal(18,2) NOT NULL,
        [MontoCapital] decimal(18,2) NOT NULL,
        [MontoInteres] decimal(18,2) NOT NULL,
        [SaldoCapital] decimal(18,2) NOT NULL,
        [Estado] nvarchar(1000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Cuotas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Cuotas_PlanesPago_PlanPagoId] FOREIGN KEY ([PlanPagoId]) REFERENCES [PlanesPago] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Cuotas_Reprogramaciones_ReprogramacionId] FOREIGN KEY ([ReprogramacionId]) REFERENCES [Reprogramaciones] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE TABLE [Pagos] (
        [Id] int NOT NULL IDENTITY,
        [SolicitudId] uniqueidentifier NULL,
        [CuentaVentaId] int NOT NULL,
        [CuotaId] int NULL,
        [MetodoPagoId] int NULL,
        [CuentaId] int NULL,
        [ConceptoPago] nvarchar(1000) NOT NULL,
        [FechaPago] date NULL,
        [Monto] decimal(18,2) NOT NULL,
        [Estado] nvarchar(1000) NOT NULL,
        [Observaciones] nvarchar(1000) NULL,
        [UsuarioId] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Pagos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Pagos_CuentasBancarias_CuentaId] FOREIGN KEY ([CuentaId]) REFERENCES [CuentasBancarias] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Pagos_CuentasVentas_CuentaVentaId] FOREIGN KEY ([CuentaVentaId]) REFERENCES [CuentasVentas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Pagos_Cuotas_CuotaId] FOREIGN KEY ([CuotaId]) REFERENCES [Cuotas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Pagos_MetodosPago_MetodoPagoId] FOREIGN KEY ([MetodoPagoId]) REFERENCES [MetodosPago] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE TABLE [Ingresos] (
        [Id] int NOT NULL IDENTITY,
        [Fecha] date NOT NULL,
        [Concepto] nvarchar(1000) NOT NULL,
        [Categoria] nvarchar(1000) NOT NULL,
        [Monto] decimal(18,2) NOT NULL,
        [Moneda] nvarchar(1000) NOT NULL,
        [Origen] nvarchar(1000) NOT NULL,
        [PagoId] int NULL,
        [CuentaVentaId] int NULL,
        [CuentaBancariaId] int NOT NULL,
        [UserId] int NOT NULL,
        [Observaciones] nvarchar(1000) NULL,
        [Estado] nvarchar(1000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Ingresos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Ingresos_CuentasBancarias_CuentaBancariaId] FOREIGN KEY ([CuentaBancariaId]) REFERENCES [CuentasBancarias] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Ingresos_CuentasVentas_CuentaVentaId] FOREIGN KEY ([CuentaVentaId]) REFERENCES [CuentasVentas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Ingresos_Pagos_PagoId] FOREIGN KEY ([PagoId]) REFERENCES [Pagos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CuentasVentas_NotaVentaId] ON [CuentasVentas] ([NotaVentaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Cuotas_PlanPagoId] ON [Cuotas] ([PlanPagoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Cuotas_ReprogramacionId] ON [Cuotas] ([ReprogramacionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Egresos_CuentaBancariaId] ON [Egresos] ([CuentaBancariaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Egresos_CuentaVentaId_Categoria] ON [Egresos] ([CuentaVentaId], [Categoria]) WHERE [CuentaVentaId] IS NOT NULL AND [Categoria] = ''COMISION_ASESOR''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Ingresos_CuentaBancariaId] ON [Ingresos] ([CuentaBancariaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Ingresos_CuentaVentaId] ON [Ingresos] ([CuentaVentaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Ingresos_PagoId] ON [Ingresos] ([PagoId]) WHERE [PagoId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Pagos_CuentaId] ON [Pagos] ([CuentaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Pagos_CuentaVentaId] ON [Pagos] ([CuentaVentaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Pagos_CuotaId] ON [Pagos] ([CuotaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Pagos_MetodoPagoId] ON [Pagos] ([MetodoPagoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Pagos_SolicitudId] ON [Pagos] ([SolicitudId]) WHERE [SolicitudId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlanesPago_CuentaVentaId] ON [PlanesPago] ([CuentaVentaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reprogramaciones_PlanPagoId] ON [Reprogramaciones] ([PlanPagoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Reprogramaciones_SolicitudId] ON [Reprogramaciones] ([SolicitudId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_VentasAnuladas_NotaVentaId] ON [VentasAnuladas] ([NotaVentaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104160311_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104160311_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

