import type { VariantProps } from 'class-variance-authority'
import { cva } from 'class-variance-authority'

export { default as ButtonGroup } from './ButtonGroup.vue'
export { default as ButtonGroupSeparator } from './ButtonGroupSeparator.vue'
export { default as ButtonGroupText } from './ButtonGroupText.vue'

export const buttonGroupVariants = cva(
  'has-[>[data-slot=button-group]]:gap-2 has-[select[aria-hidden=true]:last-child]:[&>[data-slot=select-trigger]:last-of-type]:rounded-e-md flex w-fit items-stretch *:focus-visible:relative *:focus-visible:z-10 [&>[data-slot=select-trigger]:not([class*=\'w-\'])]:w-fit [&>input]:flex-1',
  {
    variants: {
      orientation: {
        horizontal:
          '[&>[data-slot]:not(:has(~[data-slot]))]:rounded-e-md! [&>*:not(:first-child)]:rounded-s-none [&>*:not(:first-child)]:border-s-0 [&>*:not(:last-child)]:rounded-e-none',
        vertical:
          '[&>[data-slot]:not(:has(~[data-slot]))]:rounded-b-md! flex-col [&>*:not(:first-child)]:rounded-t-none [&>*:not(:first-child)]:border-t-0 [&>*:not(:last-child)]:rounded-b-none'
      }
    },
    defaultVariants: {
      orientation: 'horizontal'
    }
  }
)

export type ButtonGroupVariants = VariantProps<typeof buttonGroupVariants>
