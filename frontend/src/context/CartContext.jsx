import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useAuth } from './AuthContext';
import {
  getCart,
  addToCart as apiAddToCart,
  updateCartItemQuantity as apiUpdateQuantity,
  removeCartItem as apiRemoveItem,
  clearCart as apiClearCart,
  createBooking as createBookingApi,
} from '../services/cartService';

const CartContext = createContext(null);

export function CartProvider({ children }) {
  const { isAuthenticated, accessToken } = useAuth();
  const [cart, setCart] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const getToken = useCallback(() => {
    return accessToken || sessionStorage.getItem('ep_access_token');
  }, [accessToken]);

  const refreshCart = useCallback(async () => {
    const token = getToken();
    if (!isAuthenticated || !token) {
      setCart(null);
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const data = await getCart(token);
      setCart(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated, getToken]);

  useEffect(() => {
    if (isAuthenticated) {
      refreshCart();
    } else {
      setCart(null);
    }
  }, [isAuthenticated, refreshCart]);

  const addOrUpdateItem = async (eventId, ticketTypeId, quantity, clearExisting = false, isDelta = false) => {
    const token = getToken();
    if (!token) throw new Error('Authentication required.');

    const updatedCart = await apiAddToCart(eventId, ticketTypeId, quantity, token, clearExisting, isDelta);
    setCart(updatedCart);
    return updatedCart;
  };

  const setItemQuantity = async (ticketTypeId, quantity) => {
    const token = getToken();
    if (!token) throw new Error('Authentication required.');

    const updatedCart = await apiUpdateQuantity(ticketTypeId, quantity, token);
    setCart(updatedCart);
    return updatedCart;
  };

  const removeItem = async (ticketTypeId) => {
    const token = getToken();
    if (!token) throw new Error('Authentication required.');

    const updatedCart = await apiRemoveItem(ticketTypeId, token);
    setCart(updatedCart);
    return updatedCart;
  };

  const clearCurrentCart = async () => {
    const token = getToken();
    if (!token) throw new Error('Authentication required.');

    await apiClearCart(token);
    setCart({
      cartId: null,
      eventId: null,
      eventTitle: null,
      items: [],
      totalAmount: 0,
      totalTicketCount: 0,
    });
  };

  const cartCount = cart?.totalTicketCount || 0;

  const value = {
    cart,
    cartCount,
    loading,
    error,
    refreshCart,
    addOrUpdateItem,
    setItemQuantity,
    removeItem,
    clearCurrentCart,
    checkout: async () => {
      const token = getToken();
      if (!token) throw new Error('Authentication required.');
      if (!cart || !cart.eventId || cart.items.length === 0) {
        throw new Error('Cart is empty.');
      }
      
      const response = await createBookingApi(cart.eventId, cart.items, token);
      
      // Update local cart state to empty after successful booking
      setCart({
        cartId: null,
        eventId: null,
        eventTitle: null,
        items: [],
        totalAmount: 0,
        totalTicketCount: 0,
      });

      return response;
    }
  };

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart() {
  const context = useContext(CartContext);
  if (!context) {
    throw new Error('useCart must be used within a CartProvider');
  }
  return context;
}
