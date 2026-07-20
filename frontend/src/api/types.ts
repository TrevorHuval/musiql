export interface TokenPair {
  accessToken: string
  expiresAt: string
  refreshToken: string
  userId: string
  email: string
  tokenType: string
}

export interface Credentials {
  email: string
  password: string
}

export interface Playlist {
  id: string
  name: string
  description: string | null
  mql: string
  createdAt: string
  updatedAt: string
}

export interface PlaylistInput {
  name: string
  description?: string
  mql: string
}

export type ColumnType = 'guid' | 'string' | 'number'

export interface ResultColumn {
  name: string
  type: ColumnType
}

export type CellValue = string | number | null

export interface QueryPage {
  entity: string
  columns: ResultColumn[]
  rows: CellValue[][]
  page: number
  pageSize: number
  total: number
  hasMore: boolean
  hint: string | null
}

export interface Suggestion {
  mbid: string
  name: string
}

export type FieldType = 'string' | 'number' | 'genre'

export type Operator = '=' | '!=' | '<' | '<=' | '>' | '>=' | 'in' | 'between' | 'contains'

export interface SchemaField {
  name: string
  type: FieldType
  orderable: boolean
  operators: Operator[]
  aliases: string[]
}

export interface SchemaEntity {
  name: string
  supportsLibrary: boolean
  fields: SchemaField[]
  columns: ResultColumn[]
}

export interface SchemaResponse {
  entities: SchemaEntity[]
}

export interface MqlError {
  code: string
  message: string
  start: number
  length: number
  expected: string[] | null
}

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
  errors?: MqlError[] | Record<string, string[]>
}
