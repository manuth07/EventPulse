import React from 'react';
import { Outlet } from 'react-router-dom';
import { Header } from '../Header/Header';
import Footer from './Footer';

/**
 * Top-level application Layout wrapper
 * Enforces flex column layout (`min-h-screen flex flex-col`):
 * - Header pinned at the top
 * - Main content container has `flex-1` pushing the footer to bottom on short viewports
 * - Footer as the closing component
 */
export function Layout({
  children,
  header = null,
  showHeader = true,
  showFooter = true,
  className = '',
  style = {},
  mainClassName = '',
  mainStyle = {},
}) {
  return (
    <div
      className={`min-h-screen flex flex-col ${className}`.trim()}
      style={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        ...style,
      }}
    >
      {header || (showHeader && <Header />)}
      <main
        className={`flex-1 ${mainClassName}`.trim()}
        style={{
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          ...mainStyle,
        }}
      >
        {children || <Outlet />}
      </main>
      {showFooter && <Footer />}
    </div>
  );
}

export default Layout;
