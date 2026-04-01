import { useEffect, useState } from "react";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080";

const emptyForm = {
  email: "",
  password: "",
  fullName: "",
};

export default function App() {
  const [authForm, setAuthForm] = useState(emptyForm);
  const [token, setToken] = useState(localStorage.getItem("token") ?? "");
  const [user, setUser] = useState(readLocalJson("user"));
  const [products, setProducts] = useState([]);
  const [cart, setCart] = useState([]);
  const [orders, setOrders] = useState([]);
  const [message, setMessage] = useState("Use the seeded catalog to test a checkout flow.");

  useEffect(() => {
    loadProducts();
  }, []);

  async function loadProducts() {
    const response = await fetch(`${apiBaseUrl}/api/products`);
    const data = await response.json();
    setProducts(data);
  }

  async function register() {
    try {
      const response = await fetch(`${apiBaseUrl}/api/auth/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(authForm),
      });

      const data = await readApiResponse(response);
      persistAuth(data);
      setMessage(`Registered ${data.fullName}.`);
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  async function login() {
    try {
      const response = await fetch(`${apiBaseUrl}/api/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: authForm.email, password: authForm.password }),
      });

      const data = await readApiResponse(response);
      persistAuth(data);
      setMessage(`Logged in as ${data.fullName}.`);
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  function persistAuth(data) {
    setToken(data.token);
    setUser({ id: data.id, email: data.email, fullName: data.fullName });
    localStorage.setItem("token", data.token);
    localStorage.setItem("user", JSON.stringify({ id: data.id, email: data.email, fullName: data.fullName }));
  }

  async function addToCart(product) {
    if (!user?.id) {
      setMessage("Register or log in first.");
      return;
    }

    const existing = cart.find((item) => item.productId === product.id);
    const nextCart = existing
      ? cart.map((item) =>
          item.productId === product.id ? { ...item, quantity: item.quantity + 1 } : item,
        )
      : [
          ...cart,
          {
            productId: product.id,
            sku: product.sku,
            name: product.name,
            quantity: 1,
            unitPrice: product.price,
          },
        ];

    setCart(nextCart);

    try {
      await readApiResponse(await fetch(`${apiBaseUrl}/api/cart/${user.id}`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          userId: user.id,
          items: nextCart,
        }),
      }));
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  async function checkout() {
    if (!user?.id || cart.length === 0) {
      setMessage("Add products to the cart before checking out.");
      return;
    }

    try {
      const response = await fetch(`${apiBaseUrl}/api/orders`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          userId: user.id,
          shippingAddress: "42 Example Street, Cape Town",
          paymentToken: "demo-card",
          lines: cart.map((item) => ({
            productId: item.productId,
            quantity: item.quantity,
          })),
        }),
      });

      const data = await readApiResponse(response);
      setOrders((current) => [data, ...current]);
      setCart([]);
      setMessage(`Order ${data.orderId} submitted with idempotency hash ${data.idempotencyHash}.`);
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  function logout() {
    setToken("");
    setUser(null);
    setCart([]);
    localStorage.removeItem("token");
    localStorage.removeItem("user");
  }

  return (
    <main className="page">
      <section className="hero">
        <div>
          <p className="eyebrow">.NET 10 Microservices</p>
          <h1>MicroCommerce</h1>
          <p className="lead">
            REST through the gateway, gRPC between services, Kafka saga orchestration, Postgres plus Mongo plus Redis.
          </p>
          <p className="message">{message}</p>
        </div>
        <div className="panel auth-panel">
          <h2>Auth</h2>
          <input
            placeholder="Email"
            value={authForm.email}
            onChange={(event) => setAuthForm({ ...authForm, email: event.target.value })}
          />
          <input
            placeholder="Password"
            type="password"
            value={authForm.password}
            onChange={(event) => setAuthForm({ ...authForm, password: event.target.value })}
          />
          <input
            placeholder="Full name"
            value={authForm.fullName}
            onChange={(event) => setAuthForm({ ...authForm, fullName: event.target.value })}
          />
          <div className="button-row">
            <button onClick={register}>Register</button>
            <button onClick={login}>Login</button>
          </div>
          <div className="session">
            <strong>Session</strong>
            <div>{user ? `${user.fullName} (${user.email})` : "Signed out"}</div>
            {user && <button onClick={logout}>Logout</button>}
          </div>
        </div>
      </section>

      <section className="content-grid">
        <div className="panel">
          <h2>Products</h2>
          {products.map((product) => (
            <article className="card" key={product.id}>
              <div>
                <strong>{product.name}</strong>
                <div>{product.sku}</div>
                <p>{product.description}</p>
              </div>
              <div className="card-footer">
                <span>R {Number(product.price).toFixed(2)}</span>
                <button onClick={() => addToCart(product)}>Add</button>
              </div>
            </article>
          ))}
        </div>

        <div className="panel">
          <h2>Cart</h2>
          {cart.length === 0 && <p>Your cart is empty.</p>}
          {cart.map((item) => (
            <div className="cart-row" key={item.productId}>
              <span>{item.name}</span>
              <span>x{item.quantity}</span>
            </div>
          ))}
          <button disabled={cart.length === 0} onClick={checkout}>
            Submit Order
          </button>
        </div>

        <div className="panel">
          <h2>Recent Orders</h2>
          {orders.length === 0 && <p>No orders yet.</p>}
          {orders.map((order) => (
            <article className="order-row" key={order.orderId}>
              <strong>{order.orderId}</strong>
              <div>{order.status}</div>
              <small>{order.idempotencyHash}</small>
            </article>
          ))}
        </div>
      </section>
    </main>
  );
}

function readLocalJson(key) {
  const value = localStorage.getItem(key);
  return value ? JSON.parse(value) : null;
}

async function readApiResponse(response) {
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (response.ok) {
    return data;
  }

  const message =
    data?.message ||
    data?.errors?.join(", ") ||
    `Request failed with status ${response.status}.`;

  throw new Error(message);
}

function getErrorMessage(error) {
  return error instanceof Error ? error.message : "Something went wrong.";
}
