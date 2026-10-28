<script setup lang="ts">
import { BookOpen, ChevronsUpDown, Monitor, Moon, Palette, Sun, SunMoon } from '@lucide/vue'
import { useSidebar } from '~/components/ui/sidebar'
import { accents } from '~/composables/useAccent'

/**
 * The sidebar footer: accent colour, light or dark, and two links out. Where the blocks' `NavUser`
 * keeps the signed-in user — this sample signs every request in as one user, so there is no one to
 * show, and how the dashboard looks is the setting that is genuinely per person.
 *
 * The accent names below are NOT translated, and the same goes for role names, language codes and
 * the `api/...` badges elsewhere in the dashboard. They are identifiers: `violet` is the value
 * stored, and the name shadcn-vue's own themes page uses, so a translated "بنفسجي" in this menu
 * could not be matched to anything outside it. Copy is translated; identifiers are quoted.
 */
const colorMode = useColorMode()
const accent = useAccent()
const { isMobile } = useSidebar()

const modes = [
  { value: 'light', icon: Sun, label: 'appearance.light' },
  { value: 'dark', icon: Moon, label: 'appearance.dark' },
  { value: 'system', icon: Monitor, label: 'appearance.system' }
] as const

const currentMode = computed(() =>
  modes.find(mode => mode.value === colorMode.preference) ?? modes[2])

/**
 * The swatch beside each accent. `neutral` has no preset — it is the theme's own near-black — so
 * it is drawn as the foreground colour rather than looked up.
 */
const swatches: Record<string, string> = {
  neutral: 'var(--foreground)',
  violet: 'oklch(0.606 0.25 292.717)',
  blue: 'oklch(0.546 0.245 262.881)',
  green: 'oklch(0.648 0.2 131.684)',
  orange: 'oklch(0.705 0.213 47.604)',
  rose: 'oklch(0.645 0.246 16.439)'
}
</script>

<template>
  <SidebarMenu>
    <SidebarMenuItem>
      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <SidebarMenuButton
            size="lg"
            class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
          >
            <span class="flex size-8 shrink-0 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
              <Palette class="size-4" />
            </span>
            <span class="grid flex-1 text-start text-sm leading-tight">
              <span class="truncate font-medium">{{ $t('appearance.trigger') }}</span>
              <span class="truncate text-xs text-muted-foreground">
                {{ accent }} · {{ $t(currentMode.label) }}
              </span>
            </span>
            <ChevronsUpDown class="ms-auto size-4" />
          </SidebarMenuButton>
        </DropdownMenuTrigger>

        <DropdownMenuContent
          class="w-(--reka-dropdown-menu-trigger-width) min-w-56 rounded-lg"
          :side="isMobile ? 'bottom' : 'top'"
          align="start"
          :side-offset="4"
        >
          <DropdownMenuLabel class="text-xs text-muted-foreground">
            {{ $t('appearance.accent') }}
          </DropdownMenuLabel>
          <DropdownMenuRadioGroup v-model="accent">
            <DropdownMenuRadioItem
              v-for="name in accents"
              :key="name"
              :value="name"
              @select.prevent
            >
              <span class="size-3 rounded-full ring-1 ring-foreground/10" :style="{ background: swatches[name] }" />
              {{ name }}
            </DropdownMenuRadioItem>
          </DropdownMenuRadioGroup>

          <DropdownMenuSeparator />

          <DropdownMenuLabel class="flex items-center gap-2 text-xs text-muted-foreground">
            <SunMoon class="size-3.5" />
            {{ $t('appearance.mode') }}
          </DropdownMenuLabel>
          <DropdownMenuRadioGroup v-model="colorMode.preference">
            <DropdownMenuRadioItem
              v-for="mode in modes"
              :key="mode.value"
              :value="mode.value"
              @select.prevent
            >
              <component :is="mode.icon" />
              {{ $t(mode.label) }}
            </DropdownMenuRadioItem>
          </DropdownMenuRadioGroup>

          <DropdownMenuSeparator />

          <DropdownMenuItem as-child>
            <a href="https://www.shadcn-vue.com/docs/introduction" target="_blank" rel="noopener noreferrer">
              <BookOpen />
              {{ $t('appearance.shadcnVueDocs') }}
            </a>
          </DropdownMenuItem>
          <DropdownMenuItem as-child>
            <a href="https://github.com/dotnetboost" target="_blank" rel="noopener noreferrer">
              <GithubIcon />
              {{ $t('appearance.githubRepository') }}
            </a>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </SidebarMenuItem>
  </SidebarMenu>
</template>
