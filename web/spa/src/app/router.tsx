import type { QueryClient } from '@tanstack/react-query'
import { createBrowserRouter, createMemoryRouter, redirect, type RouteObject } from 'react-router'
import { NotFoundPage } from '@/app/not-found-page'
import { guarded } from '@/app/guarded-loader'
import { PageLoading } from '@/app/page-loading'
import { RequireRole } from '@/app/require-role'
import { RootLayout } from '@/app/root-layout'
import { RouteError } from '@/app/route-error'
import { homeFor, loadSession } from '@/lib/session'

// Session bootstrap (US-F006): the root loader asks the BFF who is signed in before anything renders.
function sessionLoader(queryClient: QueryClient) {
  return () => loadSession(queryClient)
}

// "/" sends people to their role home, or to sign-in.
function homeLoader(queryClient: QueryClient) {
  return async () => {
    const user = await loadSession(queryClient)
    return redirect(user ? homeFor(user) : '/sign-in')
  }
}

// A signed-in user who opens a public-only page goes to their role home.
function publicOnlyLoader(queryClient: QueryClient) {
  return async () => {
    const user = await loadSession(queryClient)
    return user ? redirect(homeFor(user)) : null
  }
}

export function createRoutes(queryClient: QueryClient): RouteObject[] {
  return [
    {
      path: '/',
      element: <RootLayout />,
      errorElement: <RouteError />,
      hydrateFallbackElement: <PageLoading />,
      loader: sessionLoader(queryClient),
      shouldRevalidate: () => false,
      children: [
        { index: true, loader: homeLoader(queryClient) },
        {
          path: 'sign-in',
          loader: publicOnlyLoader(queryClient),
          lazy: async () => {
            const { SignInRoute } = await import('@/features/auth/sign-in-route')
            return { Component: SignInRoute }
          },
        },
        // Anonymous access pages (AN-3..AN-8). Register, resend and forgot are public-only.
        {
          path: 'register',
          loader: publicOnlyLoader(queryClient),
          lazy: async () => {
            const { RegisterRoute } = await import('@/features/auth/register-route')
            return { Component: RegisterRoute }
          },
        },
        {
          path: 'register/check-email',
          loader: publicOnlyLoader(queryClient),
          lazy: async () => {
            const { CheckEmailRoute } = await import('@/features/auth/check-email-route')
            return { Component: CheckEmailRoute }
          },
        },
        {
          path: 'resend-confirmation',
          loader: publicOnlyLoader(queryClient),
          lazy: async () => {
            const { ResendConfirmationRoute } =
              await import('@/features/auth/resend-confirmation-route')
            return { Component: ResendConfirmationRoute }
          },
        },
        {
          path: 'forgot-password',
          loader: publicOnlyLoader(queryClient),
          lazy: async () => {
            const { ForgotPasswordRoute } = await import('@/features/auth/forgot-password-route')
            return { Component: ForgotPasswordRoute }
          },
        },
        {
          // Email links work with or without a session. The loaders read the link once and clean the URL.
          path: 'confirm-email',
          lazy: async () => {
            const { ConfirmEmailRoute, confirmEmailLoader } =
              await import('@/features/auth/confirm-email-route')
            return { Component: ConfirmEmailRoute, loader: confirmEmailLoader }
          },
        },
        {
          path: 'reset-password',
          lazy: async () => {
            const { ResetPasswordRoute, resetPasswordLoader } =
              await import('@/features/auth/reset-password-route')
            return { Component: ResetPasswordRoute, loader: resetPasswordLoader }
          },
        },
        {
          // Everything below needs a session. Code splitting by route: each page loads on first visit.
          element: <RequireRole />,
          children: [
            {
              path: 'products',
              lazy: async () => {
                const { CatalogRoute, catalogLoader } =
                  await import('@/features/catalog/catalog-route')
                return {
                  Component: CatalogRoute,
                  loader: guarded(queryClient, undefined, catalogLoader(queryClient)),
                }
              },
            },
            {
              path: 'products/:productId',
              lazy: async () => {
                const { ProductDetailRoute, productDetailLoader } =
                  await import('@/features/catalog/product-detail-route')
                return {
                  Component: ProductDetailRoute,
                  loader: guarded(queryClient, undefined, productDetailLoader(queryClient)),
                }
              },
            },
            {
              // Customer-only order pages, each a separate chunk.
              element: <RequireRole roles={['Customer']} />,
              children: [
                {
                  path: 'orders',
                  lazy: async () => {
                    const { OrdersRoute, ordersLoader } =
                      await import('@/features/orders/orders-route')
                    return {
                      Component: OrdersRoute,
                      loader: guarded(queryClient, ['Customer'], ordersLoader(queryClient)),
                    }
                  },
                },
                {
                  path: 'orders/:orderId',
                  lazy: async () => {
                    const { OrderDetailRoute, orderDetailLoader } =
                      await import('@/features/orders/order-detail-route')
                    return {
                      Component: OrderDetailRoute,
                      loader: guarded(queryClient, ['Customer'], orderDetailLoader(queryClient)),
                    }
                  },
                },
              ],
            },
            {
              // Customer-only account pages (profile, password, delete), each a separate chunk.
              path: 'account',
              element: <RequireRole roles={['Customer']} />,
              children: [
                {
                  index: true,
                  lazy: async () => {
                    const { AccountRoute, accountLoader } =
                      await import('@/features/profile/account-route')
                    return {
                      Component: AccountRoute,
                      loader: guarded(queryClient, ['Customer'], accountLoader(queryClient)),
                    }
                  },
                },
                {
                  path: 'edit',
                  lazy: async () => {
                    const { EditProfileRoute, editProfileLoader } =
                      await import('@/features/profile/edit-profile-route')
                    return {
                      Component: EditProfileRoute,
                      loader: guarded(queryClient, ['Customer'], editProfileLoader(queryClient)),
                    }
                  },
                },
                {
                  path: 'password',
                  lazy: async () => {
                    const { ChangePasswordRoute } =
                      await import('@/features/profile/change-password-route')
                    return { Component: ChangePasswordRoute }
                  },
                },
                {
                  path: 'delete',
                  lazy: async () => {
                    const { DeleteAccountRoute } =
                      await import('@/features/profile/delete-account-route')
                    return { Component: DeleteAccountRoute }
                  },
                },
              ],
            },
            {
              // Only customers have a cart; the page is a separate chunk.
              path: 'cart',
              element: <RequireRole roles={['Customer']} />,
              children: [
                {
                  index: true,
                  lazy: async () => {
                    const { CartRoute } = await import('@/features/cart/cart-route')
                    return { Component: CartRoute }
                  },
                },
              ],
            },
            {
              // Separate chunk, only for Admins. A Customer sees "not allowed" and the chunk never renders.
              path: 'admin/*',
              element: <RequireRole roles={['Admin']} />,
              children: [
                {
                  index: true,
                  lazy: async () => {
                    const { AdminRoute } = await import('@/features/admin/admin-route')
                    return { Component: AdminRoute }
                  },
                },
              ],
            },
          ],
        },
        { path: '*', element: <NotFoundPage /> },
      ],
    },
  ]
}

export function createAppRouter(queryClient: QueryClient) {
  return createBrowserRouter(createRoutes(queryClient))
}

export function createTestRouter(queryClient: QueryClient, initialEntries: string[]) {
  return createMemoryRouter(createRoutes(queryClient), { initialEntries })
}
