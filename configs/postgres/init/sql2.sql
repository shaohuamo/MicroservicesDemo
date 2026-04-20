-- switch DB
\c productsdatabase;

-- Create the table if it does not exist
CREATE TABLE public."Products"
(
    "ProductId" uuid NOT NULL,
    "ProductName" character varying(50) COLLATE pg_catalog."default" NOT NULL,
    "QuantityInStock" integer,
    "UnitPrice" numeric(10,2),
    "Version" integer NOT NULL DEFAULT 1,
    CONSTRAINT "Products_pkey" PRIMARY KEY ("ProductId")
);

-- Sample data for insertion
INSERT INTO public."Products" ("ProductId", "ProductName", "UnitPrice", "QuantityInStock")
VALUES
('1a9df78b-3f46-4c3d-9f2a-1b9f69292a77', 'Apple iPhone 15 Pro Max', 1299.99, 50),
('2c8e8e7c-97a3-4b11-9a1b-4dbe681cfe17', 'Samsung Foldable Smart Phone 2', 1499.99, 100),
('3f3e8b3a-4a50-4cd0-8d8e-1e178ae2cfc1', 'Ergonomic Office Chair', 249.99, 25),
('4c9b6f71-6c5d-485f-8db2-58011a236b63', 'Coffee Table with Storage', 179.99, 30),
('5d7e36bf-65c3-4a71-bf97-740d561d8b65', 'Samsung QLED 75 inch', 1999.99, 20),
('6a14f510-72c1-42c8-9a5a-8ef8f3f45a0d', 'Running Shoes', 49.99, 75),
('7b39ef14-932b-4c84-9187-55b748d2b28f', 'Anti-Theft Laptop Backpack', 59.99, 60),
('8c5f6e73-68fc-49d9-99b4-aecc3706a4f4', 'LG OLED 65 inch', 1499.99, 15),
('9e7e7085-6f4e-4921-8f15-c59f084080f9', 'Modern Dining Table', 699.99, 10),
('10d7b110-ecdb-4921-85a4-58a5d1b32bf4', 'PlayStation 5', 499.99, 40),
('11f2e86a-9d5d-42f9-b3c2-3e4d652e3df8', 'Executive Office Desk', 299.99, 18),
('12b369b7-9101-41b1-a653-6c6c9a4fe1e4', 'Breville Smart Blender', 129.99, 50);
