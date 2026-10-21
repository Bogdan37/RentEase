CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Amenities" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "IconName" text,
    CONSTRAINT "PK_Amenities" PRIMARY KEY ("Id")
);

CREATE TABLE "Tenants" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Slug" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Tenants" PRIMARY KEY ("Id")
);

CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "FullName" text NOT NULL,
    "Email" text NOT NULL,
    "PhoneNumber" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "TenantId" uuid,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Users_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id")
);

CREATE TABLE "Properties" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "OwnerId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Description" text NOT NULL,
    "Type" integer NOT NULL,
    "PricePerMonth" numeric NOT NULL,
    "AreaSqM" double precision NOT NULL,
    "RoomsCount" integer NOT NULL,
    "Latitude" double precision NOT NULL,
    "Longitude" double precision NOT NULL,
    "Address" text NOT NULL,
    "City" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Properties" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Properties_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Properties_Users_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "AmenityProperty" (
    "AmenitiesId" uuid NOT NULL,
    "PropertiesId" uuid NOT NULL,
    CONSTRAINT "PK_AmenityProperty" PRIMARY KEY ("AmenitiesId", "PropertiesId"),
    CONSTRAINT "FK_AmenityProperty_Amenities_AmenitiesId" FOREIGN KEY ("AmenitiesId") REFERENCES "Amenities" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AmenityProperty_Properties_PropertiesId" FOREIGN KEY ("PropertiesId") REFERENCES "Properties" ("Id") ON DELETE CASCADE
);

CREATE TABLE "BookingRequests" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "PropertyId" uuid NOT NULL,
    "ClientId" uuid NOT NULL,
    "StartDate" date NOT NULL,
    "EndDate" date NOT NULL,
    "TotalPrice" numeric NOT NULL,
    "Status" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BookingRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_BookingRequests_Properties_PropertyId" FOREIGN KEY ("PropertyId") REFERENCES "Properties" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_BookingRequests_Users_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "PropertyImages" (
    "Id" uuid NOT NULL,
    "PropertyId" uuid NOT NULL,
    "FileUrl" text NOT NULL,
    "FileName" text NOT NULL,
    "ContentType" text NOT NULL,
    "FileSizeBytes" bigint NOT NULL,
    "IsPrimary" boolean NOT NULL,
    "UploadedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PropertyImages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PropertyImages_Properties_PropertyId" FOREIGN KEY ("PropertyId") REFERENCES "Properties" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_AmenityProperty_PropertiesId" ON "AmenityProperty" ("PropertiesId");

CREATE INDEX "IX_BookingRequests_ClientId" ON "BookingRequests" ("ClientId");

CREATE INDEX "IX_BookingRequests_PropertyId" ON "BookingRequests" ("PropertyId");

CREATE INDEX "IX_Properties_OwnerId" ON "Properties" ("OwnerId");

CREATE INDEX "IX_Properties_TenantId" ON "Properties" ("TenantId");

CREATE INDEX "IX_PropertyImages_PropertyId" ON "PropertyImages" ("PropertyId");

CREATE INDEX "IX_Users_TenantId" ON "Users" ("TenantId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261020131732_InitialCreate', '9.0.21');

COMMIT;

